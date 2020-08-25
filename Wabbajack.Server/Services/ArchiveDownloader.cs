using System;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.BuildServer;
using Wabbajack.Common;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.NexusApi;
using Wabbajack.Server.DataLayer;
using Wabbajack.Server.DTOs;

namespace Wabbajack.Server.Services
{
    public class ArchiveDownloader : AbstractService<ArchiveDownloader, int>
    {
        private SqlService _sql;
        private ArchiveMaintainer _archiveMaintainer;
        private NexusApiClient _nexusClient;
        private DiscordWebHook _discord;

        public ArchiveDownloader(ILogger<ArchiveDownloader> logger, AppSettings settings, SqlService sql, ArchiveMaintainer archiveMaintainer, DiscordWebHook discord, QuickSync quickSync) 
            : base(logger, settings, quickSync, TimeSpan.FromMinutes(10))
        {
            _sql = sql;
            _archiveMaintainer = archiveMaintainer;
            _discord = discord;
        }

        public override async Task<int> Execute()
        {
            _nexusClient ??= await NexusApiClient.Get();
            int count = 0;

            while (true)
            {
                var (daily, hourly) = await _nexusClient.GetRemainingApiCalls();
                bool ignoreNexus = (daily < 100 && hourly < 10);
                //var ignoreNexus = true;
                if (ignoreNexus)
                    _logger.LogWarning($"Ignoring Nexus Downloads due to low hourly api limit (Daily: {daily}, Hourly:{hourly})");
                else
                    _logger.LogInformation($"Looking for any download (Daily: {_nexusClient.DailyRemaining}, Hourly:{_nexusClient.HourlyRemaining})");

                var nextDownload = await _sql.GetNextPendingDownload(ignoreNexus);

                if (nextDownload == null)
                    break;
                
                _logger.LogInformation($"Checking for previously archived {nextDownload.Archive.Hash}");
                
                if (nextDownload.Archive.Hash != default && _archiveMaintainer.HaveArchive(nextDownload.Archive.Hash))
                {
                    await nextDownload.Finish(_sql);
                    continue;
                }

                if (nextDownload.Archive.State is ManualDownloader.State)
                {
                    await nextDownload.Finish(_sql);
                    continue;
                }

                try
                {
                    _logger.Log(LogLevel.Information, $"Downloading {nextDownload.Archive.State.PrimaryKeyString}");
                    if (!(nextDownload.Archive.State is GameFileSourceDownloader.State)) 
                        await _discord.Send(Channel.Spam, new DiscordMessage {Content = $"Downloading {nextDownload.Archive.State.PrimaryKeyString}"});
                    await DownloadDispatcher.PrepareAll(new[] {nextDownload.Archive.State});

                    await using var tempPath = new TempFile();
                    if (!await nextDownload.Archive.State.Download(nextDownload.Archive, tempPath.Path))
                    {
                        _logger.LogError($"Downloader returned false for {nextDownload.Archive.State.PrimaryKeyString}");
                        await nextDownload.Fail(_sql, "Downloader returned false");
                        continue;
                    }

                    var hash = await tempPath.Path.FileHashAsync();
                    
                    if (nextDownload.Archive.Hash != default && hash != nextDownload.Archive.Hash)
                    {
                        _logger.Log(LogLevel.Warning, $"Downloaded archive hashes don't match for {nextDownload.Archive.State.PrimaryKeyString} {nextDownload.Archive.Hash} {nextDownload.Archive.Size} vs {hash} {tempPath.Path.Size}");
                        await nextDownload.Fail(_sql, "Invalid Hash");
                        continue;
                    }

                    if (nextDownload.Archive.Size != default &&
                        tempPath.Path.Size != nextDownload.Archive.Size)
                    {
                        await nextDownload.Fail(_sql, "Invalid Size");
                        continue;
                    }
                    nextDownload.Archive.Hash = hash;
                    nextDownload.Archive.Size = tempPath.Path.Size;

                    _logger.Log(LogLevel.Information, $"Archiving {nextDownload.Archive.State.PrimaryKeyString}");
                    await _archiveMaintainer.Ingest(tempPath.Path);

                    _logger.Log(LogLevel.Information, $"Finished Archiving {nextDownload.Archive.State.PrimaryKeyString}");
                    await nextDownload.Finish(_sql);
                    
                    if (!(nextDownload.Archive.State is GameFileSourceDownloader.State)) 
                        await _discord.Send(Channel.Spam, new DiscordMessage {Content = $"Finished downloading {nextDownload.Archive.State.PrimaryKeyString}"});


                }
                catch (Exception ex)
                {
                    _logger.Log(LogLevel.Warning, $"Error downloading {nextDownload.Archive.State.PrimaryKeyString}");
                    await nextDownload.Fail(_sql, ex.ToString());
                    await _discord.Send(Channel.Spam, new DiscordMessage {Content = $"Error downloading {nextDownload.Archive.State.PrimaryKeyString}"});
                }
                
                count++;
            }

            if (count > 0)
            {
                // Wake the Patch builder up in case it needs to build a patch now
                await _quickSync.Notify<PatchBuilder>();
            }

            return count;
        }
    }
}
