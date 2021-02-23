using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.BuildServer;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.ModListRegistry;
using Wabbajack.Lib.NexusApi;
using Wabbajack.Server.DataLayer;
using Wabbajack.Server.DTOs;

namespace Wabbajack.Server.Services
{
    public class ListValidator : AbstractService<ListValidator, int>
    {
        private SqlService _sql;
        private DiscordWebHook _discord;
        private NexusKeyMaintainance _nexus;
        private ArchiveMaintainer _archives;

        public IEnumerable<(ModListSummary Summary, DetailedStatus Detailed)> Summaries => ValidationInfo.Values.Select(e => (e.Summary, e.Detailed));
        
        public ConcurrentDictionary<string, (ModListSummary Summary, DetailedStatus Detailed, TimeSpan ValidationTime)> ValidationInfo = new();


        public ListValidator(ILogger<ListValidator> logger, AppSettings settings, SqlService sql, DiscordWebHook discord, NexusKeyMaintainance nexus, ArchiveMaintainer archives, QuickSync quickSync) 
            : base(logger, settings, quickSync, TimeSpan.FromMinutes(5))
        {
            _sql = sql;
            _discord = discord;
            _nexus = nexus;
            _archives = archives;
        }

        public override async Task<int> Execute()
        {
            var data = await _sql.GetValidationData();

            using var queue = new WorkQueue();
            var oldSummaries = Summaries;

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var results = await data.ModLists.Where(m => !m.ForceDown).PMap(queue, async metadata =>
            {
                var timer = new Stopwatch();
                timer.Start();
                var oldSummary =
                    oldSummaries.FirstOrDefault(s => s.Summary.MachineURL == metadata.Links.MachineURL);
                
                var listArchives = await _sql.ModListArchives(metadata.Links.MachineURL);
                var archives = await listArchives.PMap(queue, async archive =>
                {
                    try
                    {
                        ReportStarting(archive.State.PrimaryKeyString);
                        if (timer.Elapsed > Delay)
                        {
                            return (archive, ArchiveStatus.InValid);
                        }
                        
                        var (_, result) = await ValidateArchive(data, archive);
                        if (result == ArchiveStatus.InValid)
                        {
                            if (data.Mirrors.Contains(archive.Hash))
                                return (archive, ArchiveStatus.Mirrored);
                            return await TryToHeal(data, archive, metadata);
                        }


                        return (archive, result);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"During Validation of {archive.Hash} {archive.State.PrimaryKeyString}");
                        Utils.Log($"Exception in validation of {archive.Hash} {archive.State.PrimaryKeyString} " + ex);
                        return (archive, ArchiveStatus.InValid);
                    }
                    finally
                    {
                        ReportEnding(archive.State.PrimaryKeyString);
                    }
                });

                var failedCount = archives.Count(f => f.Item2 == ArchiveStatus.InValid);
                var passCount = archives.Count(f => f.Item2 == ArchiveStatus.Valid || f.Item2 == ArchiveStatus.Updated);
                var updatingCount = archives.Count(f => f.Item2 == ArchiveStatus.Updating);
                var mirroredCount = archives.Count(f => f.Item2 == ArchiveStatus.Mirrored);

                var summary =  new ModListSummary
                {
                    Checked = DateTime.UtcNow,
                    Failed = failedCount,
                    Passed = passCount,
                    Updating = updatingCount,
                    Mirrored = mirroredCount,
                    MachineURL = metadata.Links.MachineURL,
                    Name = metadata.Title,
                };

                var detailed = new DetailedStatus
                {
                    Name = metadata.Title,
                    Checked = DateTime.UtcNow,
                    DownloadMetaData = metadata.DownloadMetadata,
                    HasFailures = failedCount > 0,
                    MachineName = metadata.Links.MachineURL,
                    Archives = archives.Select(a => new DetailedStatusItem
                    {
                        Archive = a.Item1, 
                        IsFailing = a.Item2 == ArchiveStatus.InValid,
                        ArchiveStatus = a.Item2
                    }).ToList()
                };

                if (timer.Elapsed > Delay)
                {
                    await _discord.Send(Channel.Ham,
                        new DiscordMessage
                        {
                            Embeds = new[]
                            {
                                new DiscordEmbed
                                {
                                    Title =
                                        $"Failing {summary.Name} (`{summary.MachineURL}`) because the max validation time expired",
                                    Url = new Uri(
                                        $"https://build.wabbajack.org/lists/status/{summary.MachineURL}.html")
                                }
                            }
                        });
                }

                if (oldSummary != default && oldSummary.Summary.Failed != summary.Failed)
                {
                    _logger.Log(LogLevel.Information, $"Number of failures {oldSummary.Summary.Failed} -> {summary.Failed}");

                    if (summary.HasFailures)
                    {
                        await _discord.Send(Channel.Ham,
                            new DiscordMessage
                            {
                                Embeds = new[]
                                {
                                    new DiscordEmbed
                                    {
                                        Title =
                                            $"Number of failures in {summary.Name} (`{summary.MachineURL}`) was {oldSummary.Summary.Failed} is now {summary.Failed}",
                                        Url = new Uri(
                                            $"https://build.wabbajack.org/lists/status/{summary.MachineURL}.html")
                                    }
                                }
                            });
                    }
                    
                    if (!summary.HasFailures && oldSummary.Summary.HasFailures)
                    {
                        await _discord.Send(Channel.Ham,
                            new DiscordMessage
                            {
                                Embeds = new[]
                                {
                                    new DiscordEmbed
                                    {
                                        Title = $"{summary.Name} (`{summary.MachineURL}`) is now passing.",
                                        Url = new Uri(
                                            $"https://build.wabbajack.org/lists/status/{summary.MachineURL}.html")

                                    }
                                }
                            });
                    }

                }
                
                timer.Stop();
                

                
                ValidationInfo[summary.MachineURL] = (summary, detailed, timer.Elapsed);
                
                return (summary, detailed);
            });
            
            stopwatch.Stop();
            _logger.LogInformation($"Finished Validation in {stopwatch.Elapsed}");

            return Summaries.Count(s => s.Summary.HasFailures);
        }

        private AsyncLock _healLock = new AsyncLock();
        private async Task<(Archive, ArchiveStatus)> TryToHeal(ValidationData data, Archive archive, ModlistMetadata modList)
        {
            using var _ = await _healLock.WaitAsync();  
            var srcDownload = await _sql.GetArchiveDownload(archive.State.PrimaryKeyString, archive.Hash, archive.Size);
            if (srcDownload == null || srcDownload.IsFailed == true)
            {
                _logger.Log(LogLevel.Information, $"Cannot heal {archive.State.PrimaryKeyString} Size: {archive.Size} Hash: {(long)archive.Hash} because it hasn't been previously successfully downloaded");
                return (archive, ArchiveStatus.InValid);
            }

         
            var patches = await _sql.PatchesForSource(archive.Hash);
            foreach (var patch in patches)
            {
                if (patch.Finished is null)
                    return (archive, ArchiveStatus.Updating);

                if (patch.IsFailed == true)
                    return (archive, ArchiveStatus.InValid);
                
                var (_, status) = await ValidateArchive(data, patch.Dest.Archive);
                if (status == ArchiveStatus.Valid)
                    return (archive, ArchiveStatus.Updated);
            }


            var upgradeTime = DateTime.UtcNow;
            _logger.LogInformation($"Validator Finding Upgrade for {archive.Hash} {archive.State.PrimaryKeyString}");

            Func<Archive, Task<AbsolutePath>> resolver = async findIt =>
            {
                _logger.LogInformation($"Quick find for {findIt.State.PrimaryKeyString}");
                var foundArchive = await _sql.GetArchiveDownload(findIt.State.PrimaryKeyString);
                if (foundArchive == null)
                {
                    _logger.LogInformation($"No Quick find for {findIt.State.PrimaryKeyString}");
                    return default;
                }

                return _archives.TryGetPath(foundArchive.Archive.Hash, out var path) ? path : default;
            };
            
            var upgrade = await DownloadDispatcher.FindUpgrade(archive, resolver);
            
            
            if (upgrade == default)
            {
                _logger.Log(LogLevel.Information, $"Cannot heal {archive.State.PrimaryKeyString} because an alternative wasn't found");
                return (archive, ArchiveStatus.InValid);
            }
            
            _logger.LogInformation($"Upgrade {upgrade.Archive.State.PrimaryKeyString} found for {archive.State.PrimaryKeyString}");


            {
            }

            var found = await _sql.GetArchiveDownload(upgrade.Archive.State.PrimaryKeyString, upgrade.Archive.Hash,
                upgrade.Archive.Size);
            Guid id;
            if (found == null)
            {
                 if (upgrade.NewFile.Path.Exists)
                    await _archives.Ingest(upgrade.NewFile.Path);
                 id = await _sql.AddKnownDownload(upgrade.Archive, upgradeTime);
            }
            else
            {
                id = found.Id;
            }

            var destDownload = await _sql.GetArchiveDownload(id);

            if (destDownload.Archive.Hash == srcDownload.Archive.Hash && destDownload.Archive.State.PrimaryKeyString == srcDownload.Archive.State.PrimaryKeyString)
            {
                _logger.Log(LogLevel.Information, $"Can't heal because src and dest match");
                return (archive, ArchiveStatus.InValid);
            }

            if (destDownload.Archive.Hash == default)
            {
                _logger.Log(LogLevel.Information, "Can't heal because we got back a default hash for the downloaded file");
                return (archive, ArchiveStatus.InValid);
            }


            var existing = await _sql.FindPatch(srcDownload.Id, destDownload.Id);
            if (existing == null)
            {
                await _sql.AddPatch(new Patch {Src = srcDownload, Dest = destDownload});

                _logger.Log(LogLevel.Information,
                    $"Enqueued Patch from {srcDownload.Archive.Hash} to {destDownload.Archive.Hash}");
                await _discord.Send(Channel.Ham,
                    new DiscordMessage
                    {
                        Content =
                            $"Enqueued Patch from {srcDownload.Archive.Hash} to {destDownload.Archive.Hash} to auto-heal `{modList.Links.MachineURL}`"
                    });
            }

            await upgrade.NewFile.DisposeAsync();

            _logger.LogInformation($"Patch in progress {archive.Hash} {archive.State.PrimaryKeyString}");
            return (archive, ArchiveStatus.Updating);
        }

        private async Task<(Archive archive, ArchiveStatus)> ValidateArchive(ValidationData data, Archive archive)
        {
            switch (archive.State)
            {
                case GoogleDriveDownloader.State _:
                    // Disabled for now due to GDrive rate-limiting the build server
                    return (archive, ArchiveStatus.Valid);
                case NexusDownloader.State nexusState when data.NexusFiles.Contains((
                    nexusState.Game.MetaData().NexusGameId, nexusState.ModID, nexusState.FileID)):
                    return (archive, ArchiveStatus.Valid);
                case NexusDownloader.State ns:
                    return (archive, await FastNexusModStats(ns));
                case ManualDownloader.State _:
                    return (archive, ArchiveStatus.Valid);
                case ModDBDownloader.State _:
                    return (archive, ArchiveStatus.Valid);
                case GameFileSourceDownloader.State _:
                    return (archive, ArchiveStatus.Valid);
                case MediaFireDownloader.State _:
                    return (archive, ArchiveStatus.Valid);
                default:
                {
                    if (data.ArchiveStatus.TryGetValue((archive.State.PrimaryKeyString, archive.Hash),
                        out bool isValid))
                    {
                        return isValid ? (archive, ArchiveStatus.Valid) : (archive, ArchiveStatus.InValid);
                    }

                    return (archive, ArchiveStatus.Valid);
                }
            }
        }
        
        private AsyncLock _lock = new();

        public async Task<ArchiveStatus> FastNexusModStats(NexusDownloader.State ns)
        {
            // Check if some other thread has added them
            var mod = await _sql.GetNexusModInfoString(ns.Game, ns.ModID);
            var files = await _sql.GetModFiles(ns.Game, ns.ModID);

            if (mod == null || files == null)
            {
                // Acquire the lock
                using var lck = await _lock.WaitAsync();
                
                // Check again
                mod = await _sql.GetNexusModInfoString(ns.Game, ns.ModID);
                files = await _sql.GetModFiles(ns.Game, ns.ModID);

                if (mod == null || files == null)
                {


                    try
                    {
                        NexusApiClient nexusClient = await _nexus.GetClient();
                        var queryTime = DateTime.UtcNow;

                        if (mod == null)
                        {
                            _logger.Log(LogLevel.Information, $"Found missing Nexus mod info {ns.Game} {ns.ModID}");
                            try
                            {
                                mod = await nexusClient.GetModInfo(ns.Game, ns.ModID, false);
                            }
                            catch (Exception ex)
                            {
                                Utils.Log("Exception in Nexus Validation " + ex);
                                mod = new ModInfo
                                {
                                    mod_id = ns.ModID.ToString(),
                                    game_id = ns.Game.MetaData().NexusGameId,
                                    available = false
                                };
                            }

                            try
                            {
                                await _sql.AddNexusModInfo(ns.Game, ns.ModID, queryTime, mod);
                            }
                            catch (Exception)
                            {
                                // Could be a PK constraint failure
                            }

                        }

                        if (files == null)
                        {
                            _logger.Log(LogLevel.Information, $"Found missing Nexus mod info {ns.Game} {ns.ModID}");
                            try
                            {
                                files = await nexusClient.GetModFiles(ns.Game, ns.ModID, false);
                            }
                            catch
                            {
                                files = new NexusApiClient.GetModFilesResponse {files = new List<NexusFileInfo>()};
                            }

                            try
                            {
                                await _sql.AddNexusModFiles(ns.Game, ns.ModID, queryTime, files);
                            }
                            catch (Exception)
                            {
                                // Could be a PK constraint failure
                            }
                        }
                    }
                    catch (Exception)
                    {
                        return ArchiveStatus.InValid;
                    }
                }
            }

            if (mod.available && files.files.Any(f => !string.IsNullOrEmpty(f.category_name) && f.file_id == ns.FileID))
                return ArchiveStatus.Valid;
            return ArchiveStatus.InValid;

        }
    }
}
