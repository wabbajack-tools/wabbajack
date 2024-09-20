using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IniParser.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wabbajack.Common;
using Wabbajack.Compiler.CompilationSteps;
using Wabbajack.Downloaders;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.DTOs.Directives;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Hashing.PHash;
using Wabbajack.Installer;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using Wabbajack.VFS;

namespace Wabbajack.Compiler;

public class MO2Compiler : ACompiler
{
    public MO2Compiler(ILogger<MO2Compiler> logger, FileExtractor.FileExtractor extractor, FileHashCache hashCache,
        Context vfs,
        TemporaryFileManager manager, CompilerSettings settings, ParallelOptions parallelOptions,
        DownloadDispatcher dispatcher,
        Client wjClient, IGameLocator locator, DTOSerializer dtos, IResource<ACompiler> compilerLimiter,
        IBinaryPatchCache patchCache,
        IImageLoader imageLoader) :
        base(logger, extractor, hashCache, vfs, manager, settings, parallelOptions, dispatcher, wjClient, locator, dtos,
            compilerLimiter, patchCache, imageLoader)
    {
        MaxSteps = 14;
    }

    public static MO2Compiler Create(IServiceProvider provider, CompilerSettings mo2Settings)
    {
        return new MO2Compiler(provider.GetRequiredService<ILogger<MO2Compiler>>(),
            provider.GetRequiredService<FileExtractor.FileExtractor>(),
            provider.GetRequiredService<FileHashCache>(),
            provider.GetRequiredService<Context>(),
            provider.GetRequiredService<TemporaryFileManager>(),
            mo2Settings,
            provider.GetRequiredService<ParallelOptions>(),
            provider.GetRequiredService<DownloadDispatcher>(),
            provider.GetRequiredService<Client>(),
            provider.GetRequiredService<IGameLocator>(),
            provider.GetRequiredService<DTOSerializer>(),
            provider.GetRequiredService<IResource<ACompiler>>(),
            provider.GetRequiredService<IBinaryPatchCache>(),
            provider.GetRequiredService<IImageLoader>());
    }

    public CompilerSettings Mo2Settings => (CompilerSettings) Settings;

    public AbsolutePath MO2ModsFolder => Settings.Source.Combine(Consts.MO2ModFolderName);


    public IniData MO2Ini { get; }

    public AbsolutePath MO2ProfileDir => Settings.Source.Combine(Consts.MO2Profiles, Mo2Settings.Profile);

    public ConcurrentBag<Directive> ExtraFiles { get; private set; } = new();
    public Dictionary<AbsolutePath, IniData> ModInis { get; set; } = new();

    public static AbsolutePath GetTypicalDownloadsFolder(AbsolutePath mo2Folder)
    {
        return mo2Folder.Combine("downloads");
    }

    public override async Task<bool> Begin(CancellationToken token)
    {
        await _wjClient.SendMetric("begin_compiling", Mo2Settings.Profile);

        var roots = new List<AbsolutePath> {Settings.Source, Settings.Downloads};
        roots.AddRange(Settings.OtherGames.Append(Settings.Game).Select(g => _locator.GameLocation(g)));
        roots.Add(Settings.Downloads);

        NextStep("Initializing", "Add Roots");
        await _vfs.AddRoots(roots, token, async (cur, max) => UpdateProgressAbsolute(cur, max)); // Step 1
        
        // Find all Downloads
        IndexedArchives = await Settings.Downloads.EnumerateFiles()
            .Where(f => f.WithExtension(Ext.Meta).FileExists())
            .PMapAll(CompilerLimiter,
                async f => new IndexedArchive(_vfs.Index.ByRootPath[f])
                {
                    Name = (string) f.FileName,
                    IniData = f.WithExtension(Ext.Meta).LoadIniFile(),
                    Meta = await f.WithExtension(Ext.Meta).ReadAllTextAsync()
                }).ToList();

        await IndexGameFileHashes();

        IndexedArchives = IndexedArchives.DistinctBy(a => a.File.AbsoluteName).ToList();

        await CleanInvalidArchivesAndFillState();

        NextStep("Initializing", "Indexing Data");
        var mo2Files = Settings.Source.EnumerateFiles()
            .Where(p => p.FileExists())
            .Select(p => new RawSourceFile(_vfs.Index.ByRootPath[p], p.RelativeTo(Settings.Source)));

        // If Game Folder Files exists, ignore the game folder
        IndexedFiles = IndexedArchives.SelectMany(f => f.File.ThisAndAllChildren)
            .OrderBy(f => f.NestingFactor)
            .GroupBy(f => f.Hash)
            .ToDictionary(f => f.Key, f => f.AsEnumerable());

        AllFiles = mo2Files
            .DistinctBy(f => f.Path)
            .ToList();

        var dups = AllFiles.GroupBy(f => f.Path)
            .Where(fs => fs.Count() > 1)
            .ToList();

        if (dups.Count > 0)
        {
            _logger.LogInformation("Found {count} duplicates, exiting", dups.Count);
            return false;
        }

        ModInis = Settings.Source.Combine(Consts.MO2ModFolderName)
            .EnumerateDirectories()
            .Select(f =>
            {
                var modName = f.FileName;
                var metaPath = f.Combine("meta.ini");
                return metaPath.FileExists() ? (mod_name: f, metaPath.LoadIniFile()) : default;
            })
            .Where(f => f.Item1 != default)
            .ToDictionary(f => f.mod_name, f => f.Item2);

        ArchivesByFullPath = IndexedArchives.ToDictionary(a => a.File.AbsoluteName);


        var stack = MakeStack();

        NextStep("Compiling", "Running Compilation Stack", AllFiles.Count);
        var results = await AllFiles.PMapAllBatchedAsync(CompilerLimiter, f =>
        {
            UpdateProgress(1);
            return RunStack(stack, f);
        }).ToList();

        NextStep("Compiling", "Updating Extra files");
        // Add the extra files that were generated by the stack
        results = results.Concat(ExtraFiles).ToList();

        NextStep("Compiling", "Finding Errors");
        var noMatch = results.OfType<NoMatch>().ToArray();
        PrintNoMatches(noMatch);
        if (CheckForNoMatchExit(noMatch)) return false;

        foreach (var ignored in results.OfType<IgnoredDirectly>())
            _logger.LogInformation("Ignored {to} because {reason}", ignored.To, ignored.Reason);

        InstallDirectives = results.Where(i => i is not IgnoredDirectly).ToList();

        NextStep("Compiling", "Verifying zEdit Merges (if any)");
        zEditIntegration.VerifyMerges(this);

        await BuildPatches(token);

        await GatherArchives();

        await GatherMetaData();

        ModList = new ModList
        {
            GameType = Settings.Game,
            WabbajackVersion = Consts.CurrentMinimumWabbajackVersion,
            Archives = SelectedArchives.ToArray(),
            Directives = InstallDirectives.ToArray(),
            Name = Settings.ModListName,
            Author = Settings.ModListAuthor,
            Description = Settings.ModListDescription,
            Readme = Settings.ModListReadme,
            Image = ModListImage != default ? ModListImage.FileName : default,
            Website = Settings.ModListWebsite,
            Version = Settings.ModlistVersion,
            IsNSFW = Settings.ModlistIsNSFW
        };

        await InlineFiles(token);

        await RunValidation(ModList);

        await GenerateManifest();

        await ExportModList(token);

        ResetMembers();

        return true;
    }

    private async Task RunValidation(ModList modList)
    {
        NextStep("Finalizing", "Validating Archives", modList.Archives.Length);
        var allowList = await _wjClient.LoadDownloadAllowList();
        var mirrors = (await _wjClient.LoadMirrors()).ToLookup(a => a.Hash);
        foreach (var archive in modList.Archives)
        {
            UpdateProgress(1);
            var matchedHashes = mirrors[archive.Hash].ToArray();
            if (matchedHashes.Any())
            {
                _logger.LogInformation("Replacing {name}, {primaryKeyString} with {mirror}", archive.Name,
                    archive.State.PrimaryKeyString, matchedHashes.First().Name);
                archive.State = matchedHashes.First().State;
            }
            
            if (!_dispatcher.IsAllowed(archive, allowList))
            {
                _logger.LogCritical("Archive {name}, {primaryKeyString} is not allowed", archive.Name,
                    archive.State.PrimaryKeyString);
                throw new CompilerException("Cannot download");
            }
        }
    }


    /// <summary>
    ///     Clear references to lists that hold a lot of data.
    /// </summary>
    private void ResetMembers()
    {
        AllFiles = new List<RawSourceFile>();
        InstallDirectives = new List<Directive>();
        SelectedArchives = new List<Archive>();
        ExtraFiles = new ConcurrentBag<Directive>();
    }

    public override IEnumerable<ICompilationStep> GetStack()
    {
        return MakeStack();
    }

    /// <summary>
    ///     Creates a execution stack. The stack should be passed into Run stack. Each function
    ///     in this stack will be run in-order and the first to return a non-null result will have its
    ///     result included into the pack
    /// </summary>
    /// <returns></returns>
    public override IEnumerable<ICompilationStep> MakeStack()
    {
        NextStep("Initialization", "Generating Compilation Stack");
        _logger.LogInformation("Generating compilation stack");
        var steps = new List<ICompilationStep>
        {
            new IgnoreGameFilesIfGameFolderFilesExist(this),
            //new IncludeSteamWorkshopItems(this),
            new IgnoreSaveFiles(this),
            new IgnoreTaggedFiles(this, Settings.Ignore),
            new IgnoreInPath(this, "logs".ToRelativePath()),
            new IgnoreInPath(this, "downloads".ToRelativePath()),
            new IgnoreInPath(this, "webcache".ToRelativePath()),
            new IgnoreInPath(this, "overwrite".ToRelativePath()),
            new IgnoreInPath(this, "crashDumps".ToRelativePath()),
            new IgnorePathContains(this, "temporary_logs"),
            new IgnorePathContains(this, "GPUCache"),
            new IgnorePathContains(this, "SSEEdit Cache"),
            new IgnoreOtherProfiles(this),
            new IgnoreDisabledMods(this),
            new IncludeThisProfile(this),
            // Ignore the ModOrganizer.ini file it contains info created by MO2 on startup
            new IncludeStubbedConfigFiles(this),
            new IgnoreInPath(this, Consts.GameFolderFilesDir.Combine("Data")),
            new IgnoreInPath(this, Consts.GameFolderFilesDir.Combine("Papyrus Compiler")),
            new IgnoreInPath(this, Consts.GameFolderFilesDir.Combine("Skyrim")),
            new IgnoreRegex(this, Consts.GameFolderFilesDir + "\\\\.*\\.bsa"),
            new IncludeRegex(this, "^[^\\\\]*\\.bat$"),
            new IncludeModIniData(this),
            new DirectMatch(this),
            new IncludeTaggedFiles(this, Settings.Include),
            new IgnoreExtension(this, Ext.Pyc),
            new IgnoreExtension(this, Ext.Log),
            new PatchStockGameFiles(this, _wjClient),
            new DeconstructBSAs(
                this), // Deconstruct BSAs before building patches so we don't generate massive patch files

            new MatchSimilarTextures(this),
            new IncludePatches(this),
            new IncludeDummyESPs(this),

            // There are some types of files that will error the compilation, because they're created on-the-fly via tools
            // so if we don't have a match by this point, just drop them.
            new IgnoreExtension(this, Ext.Html),
            // Don't know why, but this seems to get copied around a bit
            new IgnoreFilename(this, "HavokBehaviorPostProcess.exe".ToRelativePath()),
            // Theme file MO2 downloads somehow
            new IncludeRegex(this, "splash\\.png"),
            // File to force MO2 into portable mode
            new IgnoreFilename(this, "portable.txt".ToRelativePath()),
            new IgnoreExtension(this, Ext.Bin),
            new IgnoreFilename(this, ".refcache".ToRelativePath()),
            //Include custom categories / splash screens
            new IncludeRegex(this, @"categories\.dat$"),
            new IncludeRegex(this, @"splash\.png"),

            new IncludeAllConfigs(this),
            // TODO
            //new zEditIntegration.IncludeZEditPatches(this),

            new IncludeTaggedFiles(this, Settings.NoMatchInclude),
            new IncludeRegex(this, ".*\\.txt"),
            new IgnorePathContains(this, @"\Edit Scripts\Export\"),
            new IgnoreExtension(this, new Extension(".CACHE")),

            // Misc
            new IncludeRegex(this, "modlist-image\\.png"),

            new DropAll(this)
        };

        if (!_settings.UseTextureRecompression)
            steps = steps.Where(s => s is not MatchSimilarTextures).ToList();

        return steps.Where(s => !s.Disabled);
    }
}