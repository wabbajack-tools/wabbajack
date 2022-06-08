using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.Common;
using Wabbajack.Compiler.CompilationSteps;
using Wabbajack.Downloaders;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.DTOs.Directives;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Installer;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using Wabbajack.VFS;

namespace Wabbajack.Compiler;

public abstract class ACompiler
{
    protected readonly DownloadDispatcher _dispatcher;
    private readonly DTOSerializer _dtos;
    private readonly FileExtractor.FileExtractor _extractor;
    private readonly FileHashCache _hashCache;
    public readonly IGameLocator _locator;
    protected internal readonly ILogger _logger;
    private readonly TemporaryFileManager _manager;
    public readonly ParallelOptions _parallelOptions;
    public readonly IBinaryPatchCache _patchCache;
    private readonly AbsolutePath _stagingFolder;
    private readonly Stopwatch _updateStopWatch = new();
    protected readonly Context _vfs;
    protected readonly Client _wjClient;
    public readonly IResource<ACompiler> CompilerLimiter;
    private int _currentStep;
    private long _currentStepProgress;

    private long _maxStepProgress;
    public ConcurrentDictionary<PatchedFromArchive, VirtualFile[]> _patchOptions;
    public CompilerSettings _settings;

    public ConcurrentDictionary<Directive, RawSourceFile> _sourceFileLinks;
    private string _statusText;
    private string _statusCategory;
    public List<IndexedArchive> IndexedArchives = new();

    public Dictionary<Hash, IEnumerable<VirtualFile>> IndexedFiles = new();

    public ModList ModList = new();
    public AbsolutePath ModListImage;

    public ACompiler(ILogger logger, FileExtractor.FileExtractor extractor, FileHashCache hashCache, Context vfs,
        TemporaryFileManager manager, CompilerSettings settings,
        ParallelOptions parallelOptions, DownloadDispatcher dispatcher, Client wjClient, IGameLocator locator,
        DTOSerializer dtos, IResource<ACompiler> compilerLimiter,
        IBinaryPatchCache patchCache)
    {
        CompilerLimiter = compilerLimiter;
        _logger = logger;
        _extractor = extractor;
        _hashCache = hashCache;
        _vfs = vfs;
        _manager = manager;
        _settings = settings;
        _stagingFolder = _manager.CreateFolder().Path;
        _parallelOptions = parallelOptions;
        _sourceFileLinks = new ConcurrentDictionary<Directive, RawSourceFile>();
        _dispatcher = dispatcher;
        _wjClient = wjClient;
        _locator = locator;
        _dtos = dtos;
        _patchOptions = new ConcurrentDictionary<PatchedFromArchive, VirtualFile[]>();
        _sourceFileLinks = new ConcurrentDictionary<Directive, RawSourceFile>();
        _patchCache = patchCache;
        _updateStopWatch = new Stopwatch();
    }

    protected long MaxSteps { get; set; }

    public CompilerSettings Settings
    {
        get => _settings;
        set => _settings = value;
    }

    public Dictionary<Game, HashSet<Hash>> GameHashes { get; set; } = new();
    public Dictionary<Hash, Game[]> GamesWithHashes { get; set; } = new();
    public ILookup<Hash,Archive> GameFiles { get; private set; }


    public bool IgnoreMissingFiles { get; set; }

    public List<Archive> SelectedArchives { get; protected set; } = new();
    public List<Directive> InstallDirectives { get; protected set; } = new();
    public List<RawSourceFile> AllFiles { get; protected set; } = new();

    public Dictionary<AbsolutePath, IndexedArchive> ArchivesByFullPath { get; set; } = new();

    public event EventHandler<StatusUpdate> OnStatusUpdate;

    public void NextStep(string statusCategory, string statusText, long maxStepProgress = 1)
    {
        _updateStopWatch.Restart();
        _maxStepProgress = maxStepProgress;
        _currentStep += 1;
        _statusText = statusText;
        _statusCategory = statusCategory;
        _logger.LogInformation("Compiler Step: {Step}", statusText);

        if (OnStatusUpdate != null)
            OnStatusUpdate(this, new StatusUpdate(statusCategory, $"[{_currentStep}/{MaxSteps}] " + statusText,
                Percent.FactoryPutInRange(_currentStep, MaxSteps),
                Percent.Zero));
    }

    public void UpdateProgress(long stepProgress)
    {
        Interlocked.Add(ref _currentStepProgress, stepProgress);

        lock (_updateStopWatch)
        {
            if (_updateStopWatch.ElapsedMilliseconds < 100) return;
            _updateStopWatch.Restart();
        }

        if (OnStatusUpdate != null)
            OnStatusUpdate(this, new StatusUpdate(_statusCategory, _statusText, Percent.FactoryPutInRange(_currentStep, MaxSteps),
                Percent.FactoryPutInRange(_currentStepProgress, _maxStepProgress)));
    }

    public abstract Task<bool> Begin(CancellationToken token);

    internal RelativePath IncludeId()
    {
        return Guid.NewGuid().ToString().ToRelativePath();
    }

    internal async Task<RelativePath> IncludeFile(byte[] data)
    {
        var id = IncludeId();
        await _stagingFolder.Combine(id).WriteAllBytesAsync(data);
        return id;
    }

    internal async Task<RelativePath> IncludeFile(Stream data)
    {
        var id = IncludeId();
        await using var os = _stagingFolder.Combine(id).Open(FileMode.Create, FileAccess.Write);
        await data.CopyToAsync(os);
        return id;
    }

    internal AbsolutePath IncludeFile(out RelativePath id)
    {
        id = IncludeId();
        return _stagingFolder.Combine(id);
    }

    internal async Task<RelativePath> IncludeFile(string data)
    {
        var id = IncludeId();
        await _stagingFolder.Combine(id).WriteAllTextAsync(data);
        return id;
    }

    internal async Task<RelativePath> IncludeFile(Stream data, CancellationToken token)
    {
        var id = IncludeId();
        await _stagingFolder.Combine(id).WriteAllAsync(data, token);
        return id;
    }

    internal async Task<RelativePath> IncludeFile(AbsolutePath data, CancellationToken token)
    {
        await using var stream = data.Open(FileMode.Open);
        return await IncludeFile(stream, token);
    }


    internal async Task<(RelativePath, AbsolutePath)> IncludeString(string str)
    {
        var id = IncludeId();
        var fullPath = _stagingFolder.Combine(id);
        await fullPath.WriteAllTextAsync(str);
        return (id, fullPath);
    }

    public async Task<bool> GatherMetaData()
    {
        _logger.LogInformation("Getting meta data for {count} archives", SelectedArchives.Count);
        NextStep("Building", "Gathering Metadata", SelectedArchives.Count);
        await SelectedArchives.PDoAll(CompilerLimiter, async a =>
        {
            UpdateProgress(1);
            await _dispatcher.FillInMetadata(a);
        });

        return true;
    }


    protected async Task IndexGameFileHashes()
    {
        NextStep("Compiling", "Indexing Game Files");
        var gameFiles = new List<Archive>();
        if (_settings.UseGamePaths)
        {
            //taking the games in Settings.IncludedGames + currently compiling game so you can eg
            //include the stock game files if you are compiling for a VR game (ex: Skyrim + SkyrimVR)
            foreach (var ag in _settings.OtherGames.Append(_settings.Game).Distinct())
                try
                {
                    if (!_locator.TryFindLocation(ag, out var path))
                    {
                        _logger.LogWarning("Game {game} was to be used in compilation but it is not installed", ag);
                        return;
                    }

                    var mainFile = ag.MetaData().MainExecutable!.Value.RelativeTo(path);

                    if (!mainFile.FileExists())
                        _logger.LogWarning("Main file {file} for {game} does not exist", mainFile, ag);

                    var versionInfo = FileVersionInfo.GetVersionInfo(mainFile.ToString());

                    var files = await _wjClient.GetGameArchives(ag, versionInfo.FileVersion ?? "0.0.0.0");
                    gameFiles.AddRange(files);

                    _logger.LogInformation($"Including {files.Length} stock game files from {ag} as download sources");
                    GameHashes[ag] = files.Select(f => f.Hash).ToHashSet();

                    IndexedArchives.AddRange(files.Select(f =>
                    {
                        var state = (GameFileSource) f.State;
                        return new IndexedArchive(
                            _vfs.Index.ByRootPath[path.Combine(state.GameFile)])
                        {
                            Name = state.GameFile.ToString().Replace("/", "_").Replace("\\", "_"),
                            State = state
                        };
                    }));
                }
                catch (Exception e)
                {
                    _logger.LogCritical(e, "Unable to find existing game files for {game}, skipping.", ag);
                }

            GamesWithHashes = GameHashes.SelectMany(g => g.Value.Select(h => (g, h)))
                .GroupBy(gh => gh.h)
                .ToDictionary(gh => gh.Key, gh => gh.Select(p => p.g.Key).ToArray());
        }

        GameFiles = gameFiles.ToLookup(f => f.Hash);
    }


    protected async Task CleanInvalidArchivesAndFillState()
    {
        NextStep("Compiling", "Cleaning Invalid Archives");
        var remove = await IndexedArchives.PKeepAll(CompilerLimiter, async a =>
        {
            try
            {
                var resolved = await ResolveArchive(a);
                if (resolved == null) return a;

                a.State = resolved.State;
                return default;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "While resolving archive {Archive}", a.Name);
                return a;
            }
        }).ToHashSet();

        if (remove.Count == 0) return;

        _logger.LogWarning(
            "Removing {count} archives from the compilation state, this is probably not an issue but reference this if you have compilation failures",
            remove.Count);
        remove.Do(r => _logger.LogWarning("Resolution failed for: ({size} {hash}) {path}", r.File.Size, r.File.Hash,
            r.File.FullPath));
        IndexedArchives.RemoveAll(a => remove.Contains(a));
    }

    protected async Task InferMetas(CancellationToken token)
    {
        async Task<bool> HasInvalidMeta(AbsolutePath filename)
        {
            var metaName = filename.WithExtension(Ext.Meta);
            if (!metaName.FileExists()) return true;

            try
            {
                var ini = metaName.LoadIniFile();
                return await _dispatcher.ResolveArchive(ini["General"].ToDictionary(d => d.KeyName, d => d.Value)) ==
                       null;
            }
            catch (Exception e)
            {
                _logger.LogCritical(e, $"Exception while checking meta {filename}");
                return true;
            }
        }

        var toFind = await _settings.Downloads.EnumerateFiles()
            .Where(f => f.Extension != Ext.Meta)
            .PMapAll(CompilerLimiter, async f => await HasInvalidMeta(f) ? f : default)
            .Where(f => f != default)
            .Where(f => f.FileExists())
            .ToList();

        NextStep("Initializing", "InferMetas", toFind.Count);
        if (toFind.Count == 0) return;

        _logger.LogInformation("Attempting to infer {count} metas from the server.", toFind.Count);

        await toFind.PDoAll(async f =>
        {
            UpdateProgress(1);
            var vf = _vfs.Index.ByRootPath[f];

            var archives = await _wjClient.GetArchivesForHash(vf.Hash);

            Archive? a = null;
            foreach (var archive in archives)
                if (await _dispatcher.Verify(archive, token))
                {
                    a = archive;
                    break;
                }

            if (a == null)
            {
                await vf.AbsoluteName.WithExtension(Ext.Meta).WriteAllLinesAsync(
                    new[]
                    {
                        "[General]",
                        "unknownArchive=true"
                    }, token);
                _logger.LogWarning("Could not infer meta for {archive} {hash}", f, vf.Hash);
                return;
            }

            _logger.LogInformation($"Inferred .meta for {vf.FullPath.FileName}, writing to disk");
            await vf.AbsoluteName.WithExtension(Ext.Meta)
                .WriteAllTextAsync(_dispatcher.MetaIniSection(a), token);
        });
    }

    protected async Task ExportModList(CancellationToken token)
    {
        NextStep("Finalizing", "Exporting Modlist");
        _logger.LogInformation("Exporting ModList to {location}", _settings.OutputFile);

        // Modify readme and ModList image to relative paths if they exist
        if (_settings.ModListImage.FileExists()) ModList.Image = (RelativePath) "modlist-image.png";

        await using (var of = _stagingFolder.Combine("modlist").Open(FileMode.Create, FileAccess.Write))
        {
            await _dtos.Serialize(ModList, of);
        }

        await _wjClient.SendModListDefinition(ModList);

        _settings.OutputFile.Delete();

        await using (var fs = _settings.OutputFile.Open(FileMode.Create, FileAccess.Write))
        {
            using var za = new ZipArchive(fs, ZipArchiveMode.Create);

            foreach (var f in _stagingFolder.EnumerateFiles())
            {
                var ze = za.CreateEntry((string) f.FileName);
                await using var os = ze.Open();
                await using var ins = f.Open(FileMode.Open);
                await ins.CopyToAsync(os, token);
            }

            // Copy in modimage
            if (_settings.ModListImage.FileExists())
            {
                var ze = za.CreateEntry((string) ModList.Image);
                await using var os = ze.Open();
                await using var ins = _settings.ModListImage.Open(FileMode.Open);
                await ins.CopyToAsync(os, token);
            }
        }

        _logger.LogInformation("Exporting Modlist metadata");
        var outputFileHash = await _hashCache.FileHashCachedAsync(_settings.OutputFile, token);
        if (outputFileHash == default)
        {
            _logger.LogCritical("Unable to hash Modlist Output File");
            return;
        }

        var metadata = new DownloadMetadata
        {
            Size = _settings.OutputFile.Size(),
            Hash = outputFileHash,
            NumberOfArchives = ModList.Archives.Length,
            SizeOfArchives = ModList.Archives.Sum(a => a.Size),
            NumberOfInstalledFiles = ModList.Directives.Length,
            SizeOfInstalledFiles = ModList.Directives.Sum(a => a.Size)
        };
        await using var metajson = _settings.OutputFile.WithExtension(new Extension(".meta.json"))
            .Open(FileMode.Create, FileAccess.Write);
        await _dtos.Serialize(metadata, metajson);

        _logger.LogInformation("Removing ModList staging folder");
        _stagingFolder.DeleteDirectory();
    }

    /// <summary>
    ///     Fills in the Patch fields in files that require them
    /// </summary>
    protected async Task BuildPatches(CancellationToken token)
    {
        var toBuild = InstallDirectives.OfType<PatchedFromArchive>()
            .Where(p => _patchOptions.GetValueOrDefault(p, Array.Empty<VirtualFile>()).Length > 0)
            .SelectMany(p => _patchOptions[p].Select(c => new PatchedFromArchive
            {
                To = p.To,
                Hash = p.Hash,
                ArchiveHashPath = c.MakeRelativePaths(),
                Size = p.Size
            }))
            .ToArray();

        NextStep("Compiling","Generating Patches", toBuild.Length);
        if (toBuild.Length == 0) return;

        // Extract all the source files
        var indexed = toBuild.GroupBy(f => _vfs.Index.FileForArchiveHashPath(f.ArchiveHashPath))
            .ToDictionary(f => f.Key);
        await _vfs.Extract(indexed.Keys.ToHashSet(),
            async (vf, sf) =>
            {
                UpdateProgress(1);
                // For each, extract the destination
                var matches = indexed[vf];
                foreach (var match in matches)
                {
                    var destFile = FindDestFile(match.To);
                    _logger.LogInformation("Patching {from} {to}", destFile, match.To);
                    // Build the patch
                    await _vfs.Extract(new[] {destFile}.ToHashSet(),
                        async (destvf, destsfn) =>
                        {

                            await using var srcStream = await sf.GetStream();
                            await using var destStream = await destsfn.GetStream();
                            using var _ = await CompilerLimiter.Begin($"Patching {match.To}", 100, token);
                            var patchSize =
                                await _patchCache.CreatePatch(srcStream, vf.Hash, destStream, destvf.Hash);
                            _logger.LogInformation("Patch size {patchSize} for {to}", patchSize, match.To);
                        }, token);
                }
            }, token);

        // Load in the patches
        await InstallDirectives.OfType<PatchedFromArchive>()
            .Where(p => p.PatchID == default)
            .PDoAll(CompilerLimiter, async pfa =>
            {
                var patches = await _patchOptions[pfa]
                    .SelectAsync(async c => (await _patchCache.GetPatch(c.Hash, pfa.Hash), c))
                    .ToList();

                // Pick the best patch
                if (patches.All(p => p.Item1 != null))
                {
                    var (patch, file) = IncludePatches.PickPatch(this, patches);
                    pfa.FromHash = file.Hash;
                    pfa.ArchiveHashPath = file.MakeRelativePaths();
                    pfa.PatchID = await IncludeFile(await patch.cache.GetData(patch));
                }
            });

        var firstFailedPatch =
            InstallDirectives.OfType<PatchedFromArchive>().FirstOrDefault(f => f.PatchID == default);
        if (firstFailedPatch != null)
        {
            _logger.LogCritical("Missing data from failed patch, starting data dump");
            _logger.LogCritical("Dest File: {to}", firstFailedPatch.To);
            _logger.LogCritical("Options ({count}):", _patchOptions[firstFailedPatch].Length);
            foreach (var choice in _patchOptions[firstFailedPatch]) _logger.LogCritical("  {path}", choice.FullPath);

            _logger.LogCritical(
                "Missing patches after generation, this should not happen. First failure: {path}", firstFailedPatch.To);
        }
    }

    private VirtualFile FindDestFile(RelativePath to)
    {
        var abs = to.RelativeTo(_settings.Source);
        if (abs.FileExists()) return _vfs.Index.ByRootPath[abs];

        if (to.InFolder(Consts.BSACreationDir))
        {
            var bsaId = (RelativePath) ((string) to).Split('\\')[1];
            var bsa = InstallDirectives.OfType<CreateBSA>().First(b => b.TempID == bsaId);
            var find = (RelativePath) Path.Combine(((string) to).Split('\\').Skip(2).ToArray());

            return _vfs.Index.ByRootPath[_settings.Source.Combine(bsa.To)].Children.First(c => c.RelativeName == find);
        }

        throw new ArgumentException($"Couldn't load data for {to}");
    }

    public async Task GenerateManifest()
    {
        NextStep("Finalizing", "Generating Manifest");
        var manifest = new Manifest(ModList);
        await using var of = _settings.OutputFile.Open(FileMode.Create, FileAccess.Write);
        await _dtos.Serialize(manifest, of);
    }

    public async Task GatherArchives()
    {
        NextStep("Building", "Gathering Archives");
        _logger.LogInformation("Building a list of archives based on the files required");

        var hashes = InstallDirectives.OfType<FromArchive>()
            .Select(a => a.ArchiveHashPath.Hash)
            .Distinct();

        var archives = IndexedArchives.OrderByDescending(f => f.File.LastModified)
            .GroupBy(f => f.File.Hash)
            .ToDictionary(f => f.Key, f => f.First());

        SelectedArchives.Clear();
        SelectedArchives.AddRange(await hashes.PMapAll(CompilerLimiter, hash =>
        {
            UpdateProgress(1);
            return ResolveArchive(hash, archives);
        }).ToList());
    }

    public async Task<Archive> ResolveArchive(Hash hash, IDictionary<Hash, IndexedArchive> archives)
    {
        if (archives.TryGetValue(hash, out var found)) return await ResolveArchive(found);

        throw new ArgumentException($"No match found for Archive sha: {hash.ToBase64()} this shouldn't happen");
    }

    public async Task<Archive?> ResolveArchive(IndexedArchive archive)
    {
        if (archive.State == null && archive.IniData == null)
        {
            _logger.LogWarning(
                "No download metadata found for {archive}, please use MO2 to query info or add a .meta file and try again.",
                archive.Name);
            return null;
        }

        IDownloadState? state;
        if (archive.State == null)
        {
            state = await _dispatcher.ResolveArchive(archive.IniData!["General"]
                .ToDictionary(d => d.KeyName, d => d.Value));

            if (state == null)
            {
                _logger.LogWarning("{archive} could not be handled by any of the downloaders", archive.Name);
                return null;
            }
        }
        else
        {
            state = archive.State;
        }

        var result = new Archive
        {
            State = state!,
            Name = archive.Name ?? "",
            Hash = archive.File.Hash,
            Size = archive.File.Size
        };

        var token = new CancellationTokenSource();
        token.CancelAfter(_settings.MaxVerificationTime);
        if (!await _dispatcher.Verify(result, token.Token))
            _logger.LogWarning(
                "Unable to resolve link for {Archive}. If this is hosted on the Nexus the file may have been removed.",
                result.State!.PrimaryKeyString);

        result.Meta = "[General]\n" + string.Join("\n", _dispatcher.MetaIni(result));
        return result;
    }

    public async Task<Directive> RunStack(IEnumerable<ICompilationStep> stack, RawSourceFile source)
    {
        foreach (var step in stack)
        {
            var result = await step.Run(source);
            if (result != null) return result;
        }

        throw new InvalidDataException("Data fell out of the compilation stack");
    }

    public abstract IEnumerable<ICompilationStep> GetStack();
    public abstract IEnumerable<ICompilationStep> MakeStack();

    public void PrintNoMatches(ICollection<NoMatch> noMatches)
    {
        const int max = 10;
        if (noMatches.Count > 0)
            foreach (var file in noMatches)
                _logger.LogWarning("     {fileTo} - {fileReason}", file.To, file.Reason);
    }

    protected async Task InlineFiles(CancellationToken token)
    {
        var grouped = ModList.Directives.OfType<InlineFile>()
            .Where(f => f.SourceDataID == default)
            .GroupBy(f => _sourceFileLinks[f].File)
            .ToDictionary(k => k.Key);

        NextStep("Building", "Inlining Files");
        if (grouped.Count == 0) return;
        await _vfs.Extract(grouped.Keys.ToHashSet(), async (vf, sfn) =>
        {
            UpdateProgress(1);
            await using var stream = await sfn.GetStream();
            var id = await IncludeFile(stream, token);
            foreach (var file in grouped[vf]) file.SourceDataID = id;
        }, token);
    }


    public bool CheckForNoMatchExit(ICollection<NoMatch> noMatches)
    {
        if (noMatches.Count > 0)
        {
            _logger.LogCritical("Exiting due to no way to compile these files");
            return true;
        }

        return false;
    }
}