using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using Wabbajack.CLI.Builder;
using Wabbajack.CLI.Services;
using Wabbajack.Common;
using Wabbajack.Compression.Zip;
using Wabbajack.Downloaders;
using Wabbajack.Downloaders.Interfaces;
using Wabbajack.DTOs;
using Wabbajack.DTOs.Configs;
using Wabbajack.DTOs.Directives;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.DTOs.ModListValidation;
using Wabbajack.DTOs.ServerResponses;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Installer;
using Wabbajack.Networking.Discord;
using Wabbajack.Networking.GitHub;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using Wabbajack.Server.Lib.TokenProviders;

namespace Wabbajack.CLI.Verbs;

public class ValidateLists
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
    private readonly HttpClient _httpClient;
    private readonly IResource<HttpClient> _httpLimiter;
    private readonly AsyncLock _imageProcessLock;

    public ValidateLists(ILogger<ValidateLists> logger, Networking.WabbajackClientApi.Client wjClient,
        Client gitHubClient, TemporaryFileManager temporaryFileManager,
        DownloadDispatcher dispatcher, DTOSerializer dtos, ParallelOptions parallelOptions,
        IFtpSiteCredentials ftpSiteCredentials, IResource<IFtpSiteCredentials> ftpRateLimiter,
        WriteOnlyClient discordClient, HttpClient httpClient, IResource<HttpClient> httpLimiter)
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
        _httpClient = httpClient;
        _httpLimiter = httpLimiter;
        _imageProcessLock = new AsyncLock();
    }

    public static VerbDefinition Definition = new("validate-lists",
        "Gets a list of modlists, validates them and exports a result list", new[]
        {
            new OptionDefinition(typeof(AbsolutePath), "r", "reports", "Location to store validation report outputs"),
            new OptionDefinition(typeof(int), "b", "batch-size", "Maximum number of lists to validate this run, prioritised by change and recency (0 = all)"),
            new OptionDefinition(typeof(int), "c", "cycle-hours", "Skip lists already validated within this many hours unless they changed (0 = disabled)"),
            new OptionDefinition(typeof(bool), "f", "only-failed", "Only re-validate lists whose last recorded status was Failed or ForcedDown")
        });

    public async Task<int> Run(AbsolutePath reports, AbsolutePath otherArchives, int batchSize, int cycleHours, bool onlyFailed)
    {
        var incremental = batchSize > 0 || cycleHours > 0 || onlyFailed;

        if (!incremental)
        {
            _logger.LogInformation("Cleaning {Reports}", reports);
            if (reports.DirectoryExists())
                reports.DeleteDirectory();
        }

        reports.CreateDirectory();
        var token = CancellationToken.None;
        
        var patchFiles = await _wjClient.GetAllPatches(token);
        _logger.LogInformation("Found {Count} patches", patchFiles.Length);

        var forcedRemovals = (await _wjClient.GetForcedRemovals(token)).ToLookup(f => f.Hash);
        _logger.LogInformation("Found {Count} forced removals", forcedRemovals.Count);

        var validationCache = new LazyCache<string, Archive, (ArchiveStatus Status, Archive archive)>
        (x => x.State.PrimaryKeyString + x.Hash,
            archive => DownloadAndValidate(archive, forcedRemovals, token));
        
        var stopWatch = Stopwatch.StartNew();
        var listData = await _wjClient.LoadLists();

        _logger.LogInformation("Found {Count} lists", listData.Length);
        foreach (var list in listData.OrderBy(d => d.NamespacedName))
        {
            _logger.LogInformation("Validating {MachineUrl} - {Version}", list.NamespacedName, list.Version);
        }

        var state = await LoadState(reports, token);
        var failingLists = onlyFailed || incremental
            ? await LoadFailingLists(reports, listData, token)
            : new HashSet<string>();

        ModlistMetadata[] toValidate;
        if (onlyFailed)
        {
            var failing = listData
                .Where(l => failingLists.Contains(l.NamespacedName))
                .OrderByDescending(l => l.DateUpdated)
                .ToList();
            toValidate = (batchSize > 0 ? failing.Take(batchSize) : failing).ToArray();
            _logger.LogInformation("Re-validating {Batch} previously-failed lists (of {Total} total)", toValidate.Length, listData.Length);
        }
        else if (incremental)
        {
            var now = DateTime.UtcNow;
            var cycle = TimeSpan.FromHours(cycleHours > 0 ? cycleHours : 4);
            var ranked = listData
                .Select(l =>
                {
                    state.TryGetValue(l.NamespacedName, out var entry);
                    var currentHash = l.DownloadMetadata?.Hash ?? default;
                    var changed = entry == null || entry.Hash != currentHash;
                    var previouslyFailed = failingLists.Contains(l.NamespacedName);
                    var recentlyValidated = entry != null && !changed && !previouslyFailed && (now - entry.ValidatedAt) < cycle;
                    return (list: l, changed, previouslyFailed, recentlyValidated);
                })
                .Where(x => !x.recentlyValidated)
                .OrderByDescending(x => x.changed)
                .ThenByDescending(x => x.previouslyFailed)
                .ThenByDescending(x => x.list.DateUpdated)
                .Select(x => x.list)
                .ToList();
            toValidate = (batchSize > 0 ? ranked.Take(batchSize) : ranked).ToArray();
            _logger.LogInformation("Validating {Batch} of {Total} lists this run", toValidate.Length, listData.Length);
        }
        else
        {
            toValidate = listData;
        }

        var batchResults = new ValidatedModList[toValidate.Length];
        await Parallel.ForEachAsync(toValidate.Select((modList, listIndex) => (modList, listIndex)), _parallelOptions,
            async (tuple, _) =>
        {
            var (modList, listIndex) = tuple;
            var validatedList = new ValidatedModList
            {
                Name = modList.Title,
                ModListHash = modList.DownloadMetadata?.Hash ?? default,
                MachineURL = modList.NamespacedName,
                Version = modList.Version
            };

            using var scope = _logger.BeginScope("MachineURL: {MachineUrl}", modList.NamespacedName);
            _logger.LogInformation("Verifying {MachineUrl} - {Title}", modList.NamespacedName, modList.Title);
            //await DownloadModList(modList, archiveManager, CancellationToken.None);

            ModList modListData;
            try
            {
                _logger.LogInformation("Loading Modlist");
                modListData =
                    await StandardInstaller.Load(_dtos, _dispatcher, modList, token);
                // Clear out the directives to save memory
                modListData.Directives = Array.Empty<Directive>();
                GC.Collect();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Forcing down {Modlist} due to error while loading: ", modList.NamespacedName);
                validatedList.Status = ListStatus.ForcedDown;
                batchResults[listIndex] = validatedList;
                return;
            }

            try
            {
                var (smallImage, largeImage) = await ProcessModlistImage(reports, modList, token);
                validatedList.SmallImage =
                    new Uri(
                        $"https://raw.githubusercontent.com/wabbajack-tools/mod-lists/master/reports/{smallImage.ToString().Replace("\\", "/")}");
                validatedList.LargeImage =
                    new Uri(
                        $"https://raw.githubusercontent.com/wabbajack-tools/mod-lists/master/reports/{largeImage.ToString().Replace("\\", "/")}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "While processing modlist images for {MachineURL}", modList.NamespacedName);
            }

            if (modList.ForceDown)
            {
                _logger.LogWarning("List is forced down");
                validatedList.Status = ListStatus.ForcedDown;
            }
            
            _logger.LogInformation("Verifying {Count} archives from {Name}", modListData.Archives.Length, modList.NamespacedName);

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
                
                return new ValidatedArchive
                {
                    Status = ArchiveStatus.InValid,
                    Original = archive
                };
            }).ToArray();

            validatedList.Archives = archives;
            validatedList.Status = archives.Any(a => a.Status == ArchiveStatus.InValid)
                ? ListStatus.Failed
                : ListStatus.Available;

            batchResults[listIndex] = validatedList;
        });
        
        var stamp = DateTime.UtcNow;
        for (var i = 0; i < toValidate.Length; i++)
        {
            state[toValidate[i].NamespacedName] = new ValidationStateEntry
            {
                ValidatedAt = stamp,
                Hash = toValidate[i].DownloadMetadata?.Hash ?? default
            };
        }

        var live = listData.Select(l => l.NamespacedName).ToHashSet();
        foreach (var staleKey in state.Keys.Where(k => !live.Contains(k)).ToList())
            state.Remove(staleKey);

        await SaveState(reports, state);

        var allArchives = batchResults.SelectMany(l => l.Archives).ToList();
        _logger.LogInformation("Validated {Count} lists in {Elapsed}", batchResults.Length, stopWatch.Elapsed);
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

        await ExportListReports(reports, batchResults, token);
        await ExportAggregates(reports, batchResults, listData, incremental, token);

        return 0;
    }

    private async Task<(RelativePath SmallImage, RelativePath LargeImage)> ProcessModlistImage(AbsolutePath reports, ModlistMetadata validatedList,
        CancellationToken token)
    {
        _logger.LogInformation("Processing Modlist Image for {MachineUrl}", validatedList.NamespacedName);
        var baseFolder = reports.Combine(validatedList.NamespacedName);
        baseFolder.CreateDirectory();

        var ms = new MemoryStream();
        await CircuitBreaker.WithAutoRetryAllAsync(_logger, async () =>
        {
            ms.SetLength(0);
            ms.Position = 0;
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(TimeSpan.FromSeconds(45));
            await using var imageStream = await _httpClient.GetStreamAsync(validatedList.Links.ImageUri, cts.Token);
            await imageStream.CopyToAsync(ms, cts.Token);
        }, maxRetries: 2);

        using var _ = await _imageProcessLock.WaitAsync();

        RelativePath smallImage, largeImage;
        ms.Position = 0;
        // Large Image
        {
            var standardWidth = 1152;
            using var image = await Image.LoadAsync(ms, token);
            var height = standardWidth * image.Height / image.Width;
            image.Mutate(x => x
                .Resize(standardWidth, height));
            largeImage = validatedList.RepositoryName.ToRelativePath().Combine(validatedList.Links.MachineURL + "_large").WithExtension(Ext.Webp);
            await image.SaveAsync(largeImage.RelativeTo(reports).ToString(), new WebpEncoder {Quality = 85}, cancellationToken: token);
        }

        ms.Position = 0;
        // Small Image
        {
            var standardWidth = 466;
            using var image = await Image.LoadAsync(ms, token);
            var height = standardWidth * image.Height / image.Width;
            image.Mutate(x => x
                .Resize(standardWidth, height));
            smallImage = validatedList.RepositoryName.ToRelativePath().Combine(validatedList.Links.MachineURL + "_small").WithExtension(Ext.Webp);
            await image.SaveAsync(smallImage.RelativeTo(reports).ToString(), new WebpEncoder {Quality = 75}, cancellationToken: token);
        }

        return (smallImage, largeImage);
    }

    private async Task SendDefinitionToLoadOrderLibrary(ModlistMetadata metadata, ModList modListData, CancellationToken token)
    {
        var lolGame = modListData.GameType switch
        {
            Game.Morrowind => 1,
            Game.Oblivion => 2,
            Game.Skyrim => 3,
            Game.SkyrimSpecialEdition => 4,
            Game.SkyrimVR => 5,
            Game.Fallout3 => 6,
            Game.FalloutNewVegas => 7,
            Game.Fallout4 => 8,
            Game.Fallout4VR => 9,
            _ => 0
        };

        if (lolGame == 0) return;
        
        var files = (await GetFiles(modListData, metadata, token))
            .Where(f => f.Key.Depth == 3)
            .Where(f => f.Key.Parent.Parent == "profiles".ToRelativePath())
            .GroupBy(f => f.Key.Parent.FileName.ToString())
            .ToArray();




        foreach (var profile in files)
        {
            var formData = new MultipartFormDataContent();
            if (files.Length > 1)
            {
                formData.Add(new StringContent(modListData.Name + $"({metadata.NamespacedName})"), "name");
            }
            else
            {
                formData.Add(new StringContent(modListData.Name + $" - Profile: {profile.Key} ({metadata.NamespacedName})"), "name");
            }

            formData.Add(new StringContent(lolGame.ToString()), "game_id");
            formData.Add(new StringContent(metadata.Description), "description");
            formData.Add(new StringContent((metadata.Version ?? Version.Parse("0.0.0.0")).ToString()), "version");
            formData.Add(new StringContent("0"), "is_private");
            formData.Add(new StringContent("perm"), "expires_at");
            if (modListData.Website != null)
            {
                formData.Add(new StringContent(modListData.Website!.ToString()), "website");
            }

            if (metadata.Links.DiscordURL != null)
            {
                formData.Add(new StringContent(metadata.Links.DiscordURL), "discord");
            }
            
            if (metadata.Links.Readme != null)
            {
                formData.Add(new StringContent(metadata.Links.Readme), "readme");
            }

            foreach (var file in profile)
            {
                formData.Add(new ByteArrayContent(file.Value), "files[]", file.Key.FileName.ToString());
            }

            using var job = await _httpLimiter.Begin("Posting to load order library", 0, token);
            var msg = new HttpRequestMessage(HttpMethod.Post, "https://api.loadorderlibrary.com/v1/lists");
            msg.Content = formData;
            using var result = await _httpClient.SendAsync(msg, token);

            if (result.IsSuccessStatusCode)
                return;

            //var data = await result.Content.ReadFromJsonAsync<string>(token);
        }

    }

    private static HashSet<RelativePath> LoadOrderFiles = new HashSet<string>()
    {
        "enblocal.ini",
        "enbseries.ini",
        "fallout.ini",
        "falloutprefs.ini",
        "fallout4.ini",
        "fallout4custom.ini",
        "fallout4prefs.ini",
        "falloutcustom.ini",
        "geckcustom.ini",
        "geckprefs.ini",
        "loadorder.txt",
        "mge.ini",
        "modlist.txt",
        "morrowind.ini",
        "mwse-version.ini",
        "oblivion.ini",
        "oblivionprefs.ini",
        "plugins.txt",
        "settings.ini",
        "skyrim.ini",
        "skyrimcustom.ini",
        "skyrimprefs.ini",
        "skyrimvr.ini",
    }.Select(f => f.ToRelativePath()).ToHashSet();


    private async Task<Dictionary<RelativePath, byte[]>> GetFiles(ModList modlist, ModlistMetadata metadata, CancellationToken token)
    {
        var archive = new Archive
        {
            State = _dispatcher.Parse(new Uri(metadata.Links.Download))!,
            Size = metadata.DownloadMetadata!.Size,
            Hash = metadata.DownloadMetadata.Hash
        };

        var stream = await _dispatcher.ChunkedSeekableStream(archive, token);
        await using var reader = new ZipReader(stream);
        var files = await reader.GetFiles();


        var indexed = files.ToDictionary(f => f.FileName.ToRelativePath());

        var entriesToGet = modlist.Directives.OfType<InlineFile>()
            .Where(f => LoadOrderFiles.Contains(f.To.FileName))
            .Select(f => (f, indexed[f.SourceDataID]))
            .ToArray();
        

        var fileData = new Dictionary<RelativePath, byte[]>();
        foreach (var entry in entriesToGet)
        {
            var ms = new MemoryStream();
            await reader.Extract(entry.Item2, ms, token);
            fileData.Add(entry.f.To, ms.ToArray());
        }

        return fileData;
    }


    private async Task ExportListReports(AbsolutePath reports, ValidatedModList[] validatedLists, CancellationToken token)
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
                var namespacedName = validatedList.MachineURL.Split('/');
                var machineURL = namespacedName[0];
                var repository = namespacedName[1];
                var oldSummary = await _wjClient.GetDetailedStatus(repository, machineURL);

                if (oldSummary.ModListHash != validatedList.ModListHash)
                {
                    try
                    {
                        await SendDefinitionToLoadOrderLibrary(validatedList, token);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("While uploading to load order library", ex);
                    }
                    
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
    }

    private async Task ExportAggregates(AbsolutePath reports, ValidatedModList[] batchResults,
        ModlistMetadata[] listData, bool incremental, CancellationToken token)
    {
        var batchByUrl = batchResults.ToDictionary(r => r.MachineURL);

        var summaries = new List<ModListSummary>();
        var allMods = new HashSet<string>();
        var modsPerList = new Dictionary<string, HashSet<string>>();
        var upgraded = new List<ValidatedArchive>();
        var seenUpgraded = new HashSet<Hash>();
        var proxyable = new HashSet<string>();

        void Accumulate(ValidatedModList l)
        {
            summaries.Add(new ModListSummary
            {
                Failed = l.Archives.Count(f => f.Status == ArchiveStatus.InValid),
                Mirrored = l.Archives.Count(f => f.Status == ArchiveStatus.Mirrored),
                Passed = l.Archives.Count(f => f.Status == ArchiveStatus.Valid),
                MachineURL = l.MachineURL,
                Name = l.Name,
                Updating = 0,
                SmallImage = l.SmallImage,
                LargeImage = l.LargeImage
            });

            var machineShort = l.MachineURL.Split('/').Last();
            var mods = new HashSet<string>();
            foreach (var archive in l.Archives)
            {
                if (archive.Original.State is Nexus n && !string.IsNullOrWhiteSpace(n.Name))
                    mods.Add(n.Name);

                if (archive.Status is ArchiveStatus.Mirrored or ArchiveStatus.Updated
                    && seenUpgraded.Add(archive.Original.Hash))
                    upgraded.Add(archive);

                if (_dispatcher.Downloader(archive.Original) is IProxyable p)
                    proxyable.Add($"{p.UnParse(archive.Original.State)}#name={archive.Original.Hash.ToHex()}");
            }

            modsPerList[machineShort] = mods;
            allMods.UnionWith(mods);
        }

        foreach (var meta in listData)
        {
            ValidatedModList? l;
            if (batchByUrl.TryGetValue(meta.NamespacedName, out var fresh))
                l = fresh;
            else if (incremental)
                l = await TryLoadStatus(reports, meta.NamespacedName, token);
            else
                l = null;

            if (l == null) continue;
            Accumulate(l);
        }

        await using (var searchIndexFile = reports.Combine("searchIndex.json")
                         .Open(FileMode.Create, FileAccess.Write, FileShare.None))
            await _dtos.Serialize(new SearchIndex { AllMods = allMods, ModsPerList = modsPerList }, searchIndexFile, true);

        await using (var summaryFile = reports.Combine("modListSummary.json")
                         .Open(FileMode.Create, FileAccess.Write, FileShare.None))
            await _dtos.Serialize(summaries.ToArray(), summaryFile, true);

        await using (var upgradedFile = reports.Combine("upgraded.json")
                         .Open(FileMode.Create, FileAccess.Write, FileShare.None))
            await _dtos.Serialize(upgraded.OrderBy(a => a.Original.Hash).ToArray(), upgradedFile, true);

        await using var proxyFile = reports.Combine("proxyable.txt")
            .Open(FileMode.Create, FileAccess.Write, FileShare.None);
        await using var tw = new StreamWriter(proxyFile);
        foreach (var line in proxyable.OrderBy(x => x))
            await tw.WriteLineAsync(line);
    }

    private async Task<ValidatedModList?> TryLoadStatus(AbsolutePath reports, string namespacedName, CancellationToken token)
    {
        var file = reports.Combine(namespacedName).Combine("status").WithExtension(Ext.Json);
        if (!file.FileExists()) return null;
        try
        {
            await using var fs = file.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
            return await _dtos.DeserializeAsync<ValidatedModList>(fs, token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read prior status for {List}", namespacedName);
            return null;
        }
    }

    private async Task<HashSet<string>> LoadFailingLists(AbsolutePath reports, ModlistMetadata[] listData, CancellationToken token)
    {
        var failing = new HashSet<string>();
        foreach (var list in listData)
        {
            var status = await TryLoadStatus(reports, list.NamespacedName, token);
            if (status != null && status.Status != ListStatus.Available)
                failing.Add(list.NamespacedName);
        }

        return failing;
    }

    private async Task<Dictionary<string, ValidationStateEntry>> LoadState(AbsolutePath reports, CancellationToken token)
    {
        var file = reports.Combine("validation-state.json");
        if (!file.FileExists()) return new Dictionary<string, ValidationStateEntry>();
        try
        {
            await using var fs = file.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
            return await _dtos.DeserializeAsync<Dictionary<string, ValidationStateEntry>>(fs, token)
                   ?? new Dictionary<string, ValidationStateEntry>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read validation state, starting fresh");
            return new Dictionary<string, ValidationStateEntry>();
        }
    }

    private async Task SaveState(AbsolutePath reports, Dictionary<string, ValidationStateEntry> state)
    {
        await using var fs = reports.Combine("validation-state.json")
            .Open(FileMode.Create, FileAccess.Write, FileShare.None);
        await _dtos.Serialize(state, fs, true);
    }
    
    private async Task SendDefinitionToLoadOrderLibrary(ValidatedModList validatedModList, CancellationToken token)
    {
        var modlistMetadata = (await _wjClient.LoadLists())
            .First(l => l.NamespacedName == validatedModList.MachineURL);
        var modList = await StandardInstaller.Load(_dtos, _dispatcher, modlistMetadata, token);
        await SendDefinitionToLoadOrderLibrary(modlistMetadata, modList, token);

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
            case MediaFire:
                return (ArchiveStatus.Valid, archive);
            case VectorPlexus:
                return (ArchiveStatus.InValid, archive);
        }

        if (archive.State is Http http && (http.Url.Host.EndsWith("github.com")
                                           //TODO: Find a better solution for the list validation of LoversLab files.
                                           || http.Url.Host.EndsWith("loverslab.com")
                                           //TODO: Find a better solution for the list validation of Mediafire files.
                                           || http.Url.Host.EndsWith("mediafire.com")))
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
            _logger.LogInformation("Previously downloaded {hash} not re-downloading", modList.NamespacedName);
            return modList.DownloadMetadata!.Hash;
        }
        else
        {
            _logger.LogInformation("Downloading {hash}", modList.NamespacedName);
            await _discord.SendAsync(Channel.Ham,
                $"Downloading and ingesting {modList.Title} ({modList.NamespacedName}) v{modList.Version}", token);
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
        _logger.LogInformation("Downloading {primaryKeyString}", state!.PrimaryKeyString);
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
}

public class ValidationStateEntry
{
    public DateTime ValidatedAt { get; set; }
    public Hash Hash { get; set; }
}
