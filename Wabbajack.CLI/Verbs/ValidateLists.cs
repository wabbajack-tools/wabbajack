using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentFTP;
using Microsoft.Extensions.Logging;
using Wabbajack.CLI.Services;
using Wabbajack.Common;
using Wabbajack.Compiler.PatchCache;
using Wabbajack.Downloaders;
using Wabbajack.Downloaders.Interfaces;
using Wabbajack.DTOs;
using Wabbajack.DTOs.Configs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.GitHub;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.DTOs.ModListValidation;
using Wabbajack.DTOs.ServerResponses;
using Wabbajack.DTOs.Validation;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Installer;
using Wabbajack.Networking.Discord;
using Wabbajack.Networking.GitHub;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using Wabbajack.Server.Lib.DTOs;
using Wabbajack.Server.Lib.TokenProviders;

namespace Wabbajack.CLI.Verbs;

public class ValidateLists : AVerb
{
    private static readonly Uri MirrorPrefix = new("https://mirror.wabbajack.org");
    private readonly WriteOnlyClient _discord;
    private readonly DownloadDispatcher _dispatcher;
    private readonly DTOSerializer _dtos;
    private readonly IResource<IFtpSiteCredentials> _ftpRateLimiter;
    private readonly IFtpSiteCredentials _ftpSiteCredentials;
    private readonly Client _gitHubClient;

    private readonly ILogger<ValidateLists> _logger;
    private readonly ParallelOptions _parallelOptions;
    private readonly Random _random;
    private readonly TemporaryFileManager _temporaryFileManager;
    private readonly Networking.WabbajackClientApi.Client _wjClient;

    public ValidateLists(ILogger<ValidateLists> logger, Networking.WabbajackClientApi.Client wjClient,
        Client gitHubClient, TemporaryFileManager temporaryFileManager,
        DownloadDispatcher dispatcher, DTOSerializer dtos, ParallelOptions parallelOptions,
        IFtpSiteCredentials ftpSiteCredentials, IResource<IFtpSiteCredentials> ftpRateLimiter,
        WriteOnlyClient discordClient)
    {
        _logger = logger;
        _wjClient = wjClient;
        _gitHubClient = gitHubClient;
        _temporaryFileManager = temporaryFileManager;
        _dispatcher = dispatcher;
        _dtos = dtos;
        _parallelOptions = parallelOptions;
        _ftpSiteCredentials = ftpSiteCredentials;
        _ftpRateLimiter = ftpRateLimiter;
        _discord = discordClient;
        _random = new Random();
    }

    public static Command MakeCommand()
    {
        var command = new Command("validate-lists");
        command.Add(new Option<List[]>(new[] {"-l", "-lists"}, "Lists of lists to validate") {IsRequired = true});
        command.Add(new Option<AbsolutePath>(new[] {"-r", "--reports"}, "Location to store validation report outputs"));
        command.Add(new Option<AbsolutePath>(new[] {"-a", "--archives"},
                "Location to store archives (files are named as the hex version of their hashes)")
            {IsRequired = true});

        command.Add(new Option<AbsolutePath>(new[] {"--other-archives"},
                "Look for files here before downloading (stored by hex hash name)")
            {IsRequired = false});

        command.Description = "Gets a list of modlists, validates them and exports a result list";
        return command;
    }

    public async Task<int> Run(List[] lists, AbsolutePath archives, AbsolutePath reports, AbsolutePath otherArchives)
    {
        reports.CreateDirectory();
        var archiveManager = new ArchiveManager(_logger, archives);
        var token = CancellationToken.None;

        _logger.LogInformation("Scanning for existing patches/mirrors");
        var mirroredFiles = (await _wjClient.GetAllMirroredFileDefinitions(token)).Select(m => m.Hash).ToHashSet();
        _logger.LogInformation("Found {Count} mirrored files", mirroredFiles.Count);
        var patchFiles = await _wjClient.GetAllPatches(token);
        _logger.LogInformation("Found {Count} patches", patchFiles.Length);

        var forcedRemovals = (await _wjClient.GetForcedRemovals(token)).ToLookup(f => f.Hash);
        _logger.LogInformation("Found {Count} forced removals", forcedRemovals.Count);

        var validationCache = new LazyCache<string, Archive, (ArchiveStatus Status, Archive archive)>
        (x => x.State.PrimaryKeyString + x.Hash,
            archive => DownloadAndValidate(archive, forcedRemovals, token));
        
        var stopWatch = Stopwatch.StartNew();
        var listData = await lists.SelectAsync(async l => await _gitHubClient.GetData(l))
            .SelectMany(l => l.Lists)
            .ToArray();

        var validatedLists = await listData.PMapAll(async modList =>
        {
            var validatedList = new ValidatedModList
            {
                Name = modList.Title,
                ModListHash = modList.DownloadMetadata?.Hash ?? default,
                MachineURL = modList.Links.MachineURL,
                Version = modList.Version
            };

            if (modList.ForceDown)
            {
                _logger.LogWarning("List is ForceDown, skipping");
                validatedList.Status = ListStatus.ForcedDown;
                return validatedList;
            }

            using var scope = _logger.BeginScope("MachineURL: {MachineUrl}", modList.Links.MachineURL);
            _logger.LogInformation("Verifying {MachineUrl} - {Title}", modList.Links.MachineURL, modList.Title);
            await DownloadModList(modList, archiveManager, CancellationToken.None);

            ModList modListData;
            try
            {
                _logger.LogInformation("Loading Modlist");
                modListData =
                    await StandardInstaller.LoadFromFile(_dtos,
                        archiveManager.GetPath(modList.DownloadMetadata!.Hash));
            }
            catch (JsonException ex)
            {
                validatedList.Status = ListStatus.ForcedDown;
                return validatedList;
            }

            _logger.LogInformation("Verifying {Count} archives", modListData.Archives.Length);

            var archives = await modListData.Archives.PMapAll(async archive =>
            {
                var result = await validationCache.Get(archive);
                if (result.Status == ArchiveStatus.Valid)
                {
                    return new ValidatedArchive
                    {
                        Status = ArchiveStatus.Valid,
                        Original = archive
                    };
                }


                if (result.Status == ArchiveStatus.InValid)
                {
                    if (mirroredFiles.Contains(archive.Hash))
                    {
                        return new ValidatedArchive
                        {
                            Status = ArchiveStatus.Mirrored,
                            Original = archive,
                            PatchedFrom = new Archive
                            {
                                State = new WabbajackCDN
                                {
                                    Url = _wjClient.GetMirrorUrl(archive.Hash)!
                                },
                                Size = archive.Size,
                                Name = archive.Name,
                                Hash = archive.Hash
                            }
                        };
                    }
                }

                if (result.Status == ArchiveStatus.InValid)
                {
                    _logger.LogInformation("Looking for patch for {Hash}", archive.Hash);
                    foreach (var file in patchFiles.Where(p => p.Original.Hash == archive.Hash && p.Status == ArchiveStatus.Updated))
                    {
                        if (await _dispatcher.Verify(file.PatchedFrom!, token))
                        {
                            return new ValidatedArchive()
                            {
                                Original = archive,
                                Status = ArchiveStatus.Updated,
                                PatchUrl = file.PatchUrl,
                                PatchedFrom = file.PatchedFrom
                            };
                        }
                    }
                }

                return new ValidatedArchive()
                {
                    Status = ArchiveStatus.InValid,
                    Original = archive
                };
            }).ToArray();

            validatedList.Archives = archives;
            validatedList.Status = archives.Any(a => a.Status == ArchiveStatus.InValid)
                ? ListStatus.Failed
                : ListStatus.Available;
            return validatedList;
        }).ToArray();

        var allArchives = validatedLists.SelectMany(l => l.Archives).ToList();
        _logger.LogInformation("Validated {Count} lists in {Elapsed}", validatedLists.Length, stopWatch.Elapsed);
        _logger.LogInformation(" - {Count} Valid", allArchives.Count(a => a.Status is ArchiveStatus.Valid));
        _logger.LogInformation(" - {Count} Invalid", allArchives.Count(a => a.Status is ArchiveStatus.InValid));
        _logger.LogInformation(" - {Count} Mirrored", allArchives.Count(a => a.Status is ArchiveStatus.Mirrored));
        _logger.LogInformation(" - {Count} Updated", allArchives.Count(a => a.Status is ArchiveStatus.Updated));

        foreach (var invalid in allArchives.Where(a => a.Status is ArchiveStatus.InValid)
            .DistinctBy(a => a.Original.Hash))
        {
            _logger.LogInformation("-- Invalid {Hash}: {PrimaryKeyString}", invalid.Original.Hash.ToHex(),
                invalid.Original.State.PrimaryKeyString);
        }

        await ExportReports(reports, validatedLists, token);
        
        
        var usedMirroredFiles = validatedLists.SelectMany(a => a.Archives)
            .Where(m => m.Status == ArchiveStatus.Mirrored)
            .Select(m => m.Original.Hash)
            .ToHashSet();
        await DeleteOldMirrors(mirroredFiles, usedMirroredFiles);

        return 0;
    }


    private async Task ExportReports(AbsolutePath reports, ValidatedModList[] validatedLists, CancellationToken token)
    {
        foreach (var validatedList in validatedLists)
        {
            var baseFolder = reports.Combine(validatedList.MachineURL);
            baseFolder.CreateDirectory();
            await using var jsonFile = baseFolder.Combine("status").WithExtension(Ext.Json)
                .Open(FileMode.Create, FileAccess.Write, FileShare.None);
            await _dtos.Serialize(validatedList, jsonFile, true);

            await using var mdFile = baseFolder.Combine("status").WithExtension(Ext.Md)
                .Open(FileMode.Create, FileAccess.Write, FileShare.None);
            await using var sw = new StreamWriter(mdFile);

            await sw.WriteLineAsync($"## Validation Report - {validatedList.Name} ({validatedList.MachineURL})");
            await sw.WriteAsync("\n\n");

            async Task WriteSection(TextWriter w, ArchiveStatus status, string sectionName)
            {
                var archives = validatedList.Archives.Where(a => a.Status == status).ToArray();
                await w.WriteLineAsync($"### {sectionName} ({archives.Length})");

                foreach (var archive in archives.OrderBy(a => a.Original.Name))
                {
                    if (_dispatcher.TryGetDownloader(archive.Original, out var downloader) &&
                        downloader is IUrlDownloader u)
                    {
                        await w.WriteLineAsync(
                            $"*  [{archive.Original.Name}]({u.UnParse(archive.Original.State)})");
                    }
                    else
                    {
                        await w.WriteLineAsync(
                            $"*  {archive.Original.Name}");
                    }
                }
            }

            await WriteSection(sw, ArchiveStatus.InValid, "Invalid");
            await WriteSection(sw, ArchiveStatus.Updated, "Updated");
            await WriteSection(sw, ArchiveStatus.Mirrored, "Mirrored");
            await WriteSection(sw, ArchiveStatus.Valid, "Valid");


            try
            {
                var oldSummary = await _wjClient.GetDetailedStatus(validatedList.MachineURL);

                if (oldSummary.ModListHash != validatedList.ModListHash)
                {
                    await _discord.SendAsync(Channel.Ham,
                        $"Finished processing {validatedList.Name} ({validatedList.MachineURL}) v{validatedList.Version} ({oldSummary.ModListHash} -> {validatedList.ModListHash})",
                        token);
                }

                if (oldSummary.Failures != validatedList.Failures)
                {
                    if (validatedList.Failures == 0)
                    {
                        await _discord.SendAsync(Channel.Ham,
                            new DiscordMessage
                            {
                                Embeds = new[]
                                {
                                    new DiscordEmbed
                                    {
                                        Title =
                                            $"{validatedList.Name} (`{validatedList.MachineURL}`) is now passing.",
                                        Url = new Uri(
                                            $"https://github.com/wabbajack-tools/mod-lists/blob/master/reports/{validatedList.MachineURL}/status.md")
                                    }
                                }
                            }, token);
                    }
                    else
                    {
                        await _discord.SendAsync(Channel.Ham,
                            new DiscordMessage
                            {
                                Embeds = new[]
                                {
                                    new DiscordEmbed
                                    {
                                        Title =
                                            $"Number of failures in {validatedList.Name} (`{validatedList.MachineURL}`) was {oldSummary.Failures} is now {validatedList.Failures}",
                                        Url = new Uri(
                                            $"https://github.com/wabbajack-tools/mod-lists/blob/master/reports/{validatedList.MachineURL}/status.md")
                                    }
                                }
                            }, token);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "While sending discord message for {MachineURl}", validatedList.MachineURL);
            }
        }


        var summaries = validatedLists.Select(l => new ModListSummary
        {
            Failed = l.Archives.Count(f => f.Status == ArchiveStatus.InValid),
            Mirrored = l.Archives.Count(f => f.Status == ArchiveStatus.Mirrored),
            Passed = l.Archives.Count(f => f.Status == ArchiveStatus.Valid),
            MachineURL = l.MachineURL,
            Name = l.Name,
            Updating = 0
        }).ToArray();


        await using var summaryFile = reports.Combine("modListSummary.json")
            .Open(FileMode.Create, FileAccess.Write, FileShare.None);
        await _dtos.Serialize(summaries, summaryFile, true);


        var upgradedMetas = validatedLists.SelectMany(v => v.Archives)
            .Where(a => a.Status is ArchiveStatus.Mirrored or ArchiveStatus.Updated)
            .DistinctBy(a => a.Original.Hash)
            .OrderBy(a => a.Original.Hash)
            .ToArray();
        await using var upgradedMetasFile = reports.Combine("upgraded.json")
            .Open(FileMode.Create, FileAccess.Write, FileShare.None);
        await _dtos.Serialize(upgradedMetas, upgradedMetasFile, true);


    }

    private async Task DeleteOldMirrors(IEnumerable<Hash> mirroredFiles, IReadOnlySet<Hash> usedMirroredFiles)
    {
        foreach (var file in mirroredFiles.Where(file => !usedMirroredFiles.Contains(file)))
        {
            await _wjClient.DeleteMirror(file);
        }
    }

    private async Task<(ArchiveStatus, Archive)> DownloadAndValidate(Archive archive,
        ILookup<Hash, ForcedRemoval> forcedRemovals, CancellationToken token)
    {
        if (forcedRemovals.Contains(archive.Hash))
            return (ArchiveStatus.InValid, archive);
        
        switch (archive.State)
        {
            case GameFileSource:
                return (ArchiveStatus.Valid, archive);
            case Manual:
                return (ArchiveStatus.Valid, archive);
            case TESAlliance:
                return (ArchiveStatus.Valid, archive);
            case Mega:
                return (ArchiveStatus.Valid, archive);
            case Nexus:
                return (ArchiveStatus.Valid, archive);
            case VectorPlexus:
                return (ArchiveStatus.Valid, archive);
        }

        if (archive.State is Http http && http.Url.Host.EndsWith("github.com"))
            return (ArchiveStatus.Valid, archive);
        
        try
        {
            for (var attempts = 0; attempts < 3; attempts++)
            {
                var valid = await _dispatcher.Verify(archive, token);
                if (valid)
                    return (ArchiveStatus.Valid, archive);
                var delay = _random.Next(200, 1200);
                _logger.LogWarning(
                    "Archive {primaryKeyString} is invalid retrying in {Delay} ms ({Attempt} of {MaxAttempts})",
                    archive.State.PrimaryKeyString, delay, attempts, 3);
                await Task.Delay(delay, token);
            }

            _logger.LogWarning("Archive {primaryKeyString} is invalid", archive.State.PrimaryKeyString);
            return (ArchiveStatus.InValid, archive);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "While verifying {primaryKeyString}", archive.State.PrimaryKeyString);
            return (ArchiveStatus.InValid, archive);
        }
    }

    private async Task<Hash> DownloadModList(ModlistMetadata modList, ArchiveManager archiveManager,
        CancellationToken token)
    {
        if (archiveManager.HaveArchive(modList.DownloadMetadata!.Hash))
        {
            _logger.LogInformation("Previously downloaded {hash} not re-downloading", modList.Links.MachineURL);
            return modList.DownloadMetadata!.Hash;
        }
        else
        {
            _logger.LogInformation("Downloading {hash}", modList.Links.MachineURL);
            await _discord.SendAsync(Channel.Ham,
                $"Downloading and ingesting {modList.Title} ({modList.Links.MachineURL}) v{modList.Version}", token);
            return await DownloadWabbajackFile(modList, archiveManager, token);
        }
    }

    private async Task<Hash> DownloadWabbajackFile(ModlistMetadata modList, ArchiveManager archiveManager,
        CancellationToken token)
    {
        var state = _dispatcher.Parse(new Uri(modList.Links.Download));
        if (state == null)
            _logger.LogCritical("Can't download {url}", modList.Links.Download);

        var archive = new Archive
        {
            State = state!,
            Size = modList.DownloadMetadata!.Size,
            Hash = modList.DownloadMetadata.Hash
        };

        await using var tempFile = _temporaryFileManager.CreateFile(Ext.Wabbajack);
        _logger.LogInformation("Downloading {primaryKeyString}", state.PrimaryKeyString);
        var hash = await _dispatcher.Download(archive, tempFile.Path, token);

        if (hash != modList.DownloadMetadata.Hash)
        {
            _logger.LogCritical("Downloaded modlist was {actual} expected {expected}", hash,
                modList.DownloadMetadata.Hash);
            throw new Exception();
        }

        _logger.LogInformation("Archiving {hash}", hash);
        await archiveManager.Ingest(tempFile.Path, token);
        return hash;
    }

    public async ValueTask<HashSet<Hash>> AllMirroredFiles(CancellationToken token)
    {
        using var client = await GetMirrorFtpClient(token);
        using var job = await _ftpRateLimiter.Begin("Getting mirror list", 0, token);
        var files = await client.GetListingAsync(token);
        var parsed = files.TryKeep(f => (Hash.TryGetFromHex(f.Name, out var hash), hash)).ToHashSet();
        return parsed;
    }

    public async ValueTask<HashSet<(Hash, Hash)>> AllPatchFiles(CancellationToken token)
    {
        using var client = await GetPatchesFtpClient(token);
        using var job = await _ftpRateLimiter.Begin("Getting patches list", 0, token);
        var files = await client.GetListingAsync(token);
        var parsed = files.TryKeep(f =>
            {
                var parts = f.Name.Split("_");
                return (parts.Length == 2, parts);
            })
            .TryKeep(p => (Hash.TryGetFromHex(p[0], out var fromHash) &
                           Hash.TryGetFromHex(p[1], out var toHash),
                (fromHash, toHash)))
            .ToHashSet();
        return parsed;
    }

    private async Task<FtpClient> GetMirrorFtpClient(CancellationToken token)
    {
        var client = await (await _ftpSiteCredentials.Get())![StorageSpace.Mirrors].GetClient(_logger);
        await client.ConnectAsync(token);
        return client;
    }

    private async Task<FtpClient> GetPatchesFtpClient(CancellationToken token)
    {
        var client = await (await _ftpSiteCredentials.Get())![StorageSpace.Patches].GetClient(_logger);
        await client.ConnectAsync(token);
        return client;
    }

    protected override ICommandHandler GetHandler()
    {
        return CommandHandler.Create(Run);
    }
}