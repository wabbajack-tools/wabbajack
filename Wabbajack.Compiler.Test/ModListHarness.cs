using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wabbajack.Common;
using Wabbajack.Downloaders;
using Wabbajack.DTOs;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Installer;
using Wabbajack.Installer.Preflight;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Xunit;

namespace Wabbajack.Compiler.Test;

public class ModListHarness
{
    private readonly DownloadDispatcher _downloadDispatcher;
    private readonly AbsolutePath _downloadPath;
    private readonly DTOSerializer _dtos;
    public readonly FileExtractor.FileExtractor _fileExtractor;
    private readonly AbsolutePath _gameFolder;
    private readonly AbsolutePath _installDownloads;
    private readonly AbsolutePath _watchFolder;
    private readonly AbsolutePath _installLocation;
    private readonly ILogger<ModListHarness> _logger;
    private readonly TemporaryFileManager _manager;
    private readonly Dictionary<RelativePath, Mod> _mods;
    private readonly AbsolutePath _modsFolder;
    private readonly AbsolutePath _outputFile;
    private readonly TemporaryPath _outputFolder;
    private readonly string _profileName;
    private readonly IServiceProvider _serviceProvider;
    public readonly AbsolutePath _source;

    public ModListHarness(ILogger<ModListHarness> logger, TemporaryFileManager manager,
        FileExtractor.FileExtractor fileExtractor, IServiceProvider serviceProvider,
        DownloadDispatcher downloadDispatcher, DTOSerializer dtos)
    {
        _logger = logger;
        _manager = manager;
        _source = _manager.CreateFolder();
        _profileName = Guid.NewGuid().ToString();
        _downloadPath = _manager.CreateFolder();
        _installLocation = _manager.CreateFolder();
        _outputFolder = _manager.CreateFolder();
        _modsFolder = _source.Combine(Consts.MO2ModFolderName);
        _mods = new Dictionary<RelativePath, Mod>();
        _fileExtractor = fileExtractor;
        _serviceProvider = serviceProvider;
        _downloadDispatcher = downloadDispatcher;
        _gameFolder = _manager.CreateFolder();
        _outputFile = _outputFolder.Path.Combine(_profileName + ".wabbajack");

        _installDownloads = _installLocation.Combine("downloads");
        _watchFolder = _manager.CreateFolder();
        _dtos = dtos;
    }

    public Mod AddMod(string? name = null)
    {
        name ??= Guid.NewGuid().ToString();
        var mod = new Mod(name.ToRelativePath(), _modsFolder.Combine(name), this, new HashSet<string>());
        _mods[name!.ToRelativePath()] = mod;
        return mod;
    }

    public async Task<ModList?> CompileAndInstall(Action<CompilerSettings>? configureSettings = null)
    {
        var modlist = await Compile(configureSettings);
        await Install();

        return modlist;
    }

    public async Task<ModList?> Compile(Action<CompilerSettings>? configureSettings = null)
    {
        configureSettings ??= x => { };

        _source.Combine(Consts.MO2Profiles, _profileName).CreateDirectory();
        using var scope = _serviceProvider.CreateScope();
        var settings = scope.ServiceProvider.GetService<CompilerSettings>()!;
        settings.Downloads = _downloadPath;
        settings.Game = Game.SkyrimSpecialEdition;
        settings.Source = _source;
        settings.ModListName = _profileName;
        settings.Profile = _profileName;
        settings.OutputFile = _outputFile;
        settings.UseTextureRecompression = true;
        configureSettings(settings);

        var modLines = _mods.Select(
            m => (m.Value.EnabledIn.Contains(_profileName) ? "+" : "-") + m.Key);
        await _source.Combine(Consts.MO2Profiles, _profileName, Consts.ModListTxt)
            .WriteAllLinesAsync(modLines,
                CancellationToken.None);

        var compiler = scope.ServiceProvider.GetService<MO2Compiler>();
        if (!await compiler!.Begin(CancellationToken.None))
            return null;

        var modlist = await StandardInstaller.LoadFromFile(_dtos, settings.OutputFile);
        return modlist;
    }

    public async Task<bool> Install()
    {
        // The installer no longer downloads: what compiling used has to be in place before Begin.
        await PrePlaceDownloads();
        using var scope = _serviceProvider.CreateScope();
        var settings = await ConfigureInstall(scope.ServiceProvider);
        var installer = scope.ServiceProvider.GetService<StandardInstaller>()!;

        return await installer.Begin(CancellationToken.None) == InstallResult.Succeeded;
    }

    /// <summary>
    ///     Runs the preflight checklist against the compiled list, the way the installer hosts do before
    ///     <see cref="Install" />. Defaults to the CLI's options: no waiting for manual downloads, no metrics.
    ///     The game folder gets the game's required files (empty) so the game-files check has something to find.
    /// </summary>
    public async Task<PreflightOutcome> Preflight(PreflightOptions? options = null)
    {
        foreach (var required in Game.SkyrimSpecialEdition.MetaData().RequiredFiles)
        {
            var file = _gameFolder.Combine(required);
            if (!file.FileExists())
                await file.WriteAllTextAsync("");
        }

        await using var scope = _serviceProvider.CreateAsyncScope();
        var settings = await ConfigureInstall(scope.ServiceProvider);
        var runner = PreflightRunner.Create(scope.ServiceProvider, settings,
            options ?? new PreflightOptions {WaitForManualDownloads = false, SendMetrics = false, WatchFolder = _watchFolder});
        return await runner.RunAll(CancellationToken.None);
    }

    /// <summary>
    ///     Copies what compiling used into the install's downloads folder, so an install (or a preflight)
    ///     finds every archive already in place.
    /// </summary>
    public async Task PrePlaceDownloads()
    {
        _installDownloads.CreateDirectory();
        foreach (var file in _downloadPath.EnumerateFiles())
            await file.CopyToAsync(_installDownloads.Combine(file.FileName), CancellationToken.None);
    }

    private async Task<InstallerConfiguration> ConfigureInstall(IServiceProvider scoped)
    {
        var settings = scoped.GetService<InstallerConfiguration>()!;
        settings.Install = _installLocation;
        settings.Downloads = _installDownloads;
        settings.ModList = await StandardInstaller.LoadFromFile(_dtos, _outputFile);
        settings.ModlistArchive = _outputFile;
        settings.Game = Game.SkyrimSpecialEdition;
        settings.GameFolder = _gameFolder;
        settings.SystemParameters = new SystemParameters
        {
            ScreenWidth = 1920,
            ScreenHeight = 1080,
            SystemMemorySize = 8L * 1024 * 1024 * 1024,
            SystemPageSize = 8L * 1024 * 1024 * 1024,
            VideoMemorySize = 8L * 1024 * 1024 * 1024
        };
        return settings;
    }

    /// <summary>
    ///     Adds a file the list will record as a manual download. The compiler checks every URL against the
    ///     server allow-list, so the default is a page under an allowed prefix; nothing is ever fetched from it.
    /// </summary>
    public async Task<Uri> AddManualDownload(AbsolutePath path, Uri? url = null)
    {
        url ??= new Uri($"https://skse.silverlock.org/beta/{path.FileName}");
        var toPath = path.FileName.RelativeTo(_downloadPath);
        await path.CopyToAsync(toPath, CancellationToken.None);

        await toPath.WithExtension(Ext.Meta)
            .WriteAllLinesAsync(new[] {"[General]", $"manualURL={url}"}, CancellationToken.None);
        return url;
    }

    /// <summary>Path of a file in the folder the compiler reads archives from.</summary>
    public AbsolutePath DownloadPath(string fileName)
    {
        return _downloadPath.Combine(fileName);
    }

    /// <summary>Path of a file in the folder the install reads archives from.</summary>
    public AbsolutePath InstallDownloadPath(string fileName)
    {
        return _installDownloads.Combine(fileName);
    }

    public async Task<Mod> InstallMod(Extension ext, Uri uri)
    {
        var state = _downloadDispatcher.Parse(uri);
        var file = (Guid.NewGuid() + ext.ToString()).ToRelativePath();
        var archive = new Archive {State = state!, Name = file.ToString()};
        _logger.LogInformation("Downloading: {uri}", uri);
        await _downloadDispatcher.Download(archive, file.RelativeTo(_downloadPath), CancellationToken.None);
        await file.WithExtension(Ext.Meta).RelativeTo(_downloadPath)
            .WriteAllTextAsync(_downloadDispatcher.MetaIniSection(archive), CancellationToken.None);

        var mod = AddMod(file.WithoutExtension().ToString());
        mod.EnabledIn.Add(_profileName);

        _logger.LogInformation("Extracting: {file}", file);
        await mod.AddFromArchive(file.RelativeTo(_downloadPath));
        return mod;
    }

    public void VerifyInstalledFile(AbsolutePath source)
    {
        var dest = source.RelativeTo(_source).RelativeTo(_installLocation);
        _logger.LogInformation("Verifying {file}", source.RelativeTo(_source));
        Assert.Equal(source.Size(), dest.Size());
    }
}

public record Mod(RelativePath Name, AbsolutePath FullPath, ModListHarness Harness, HashSet<string> EnabledIn)
{
    public async Task<AbsolutePath> AddFile(AbsolutePath src)
    {
        var dest = FullPath.Combine(src.FileName);
        await src.CopyToAsync(dest, CancellationToken.None);
        return dest;
    }

    public async Task AddFromArchive(AbsolutePath src)
    {
        await Harness._fileExtractor.ExtractAll(src, FullPath, CancellationToken.None);
    }

    public async Task<AbsolutePath> AddData(RelativePath path, string data)
    {
        var fullPath = FullPath.Combine(path);
        fullPath.Parent.CreateDirectory();
        await fullPath.WriteAllTextAsync(data);
        return fullPath;
    }
}