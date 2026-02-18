using System;
using System.IO;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Media.Imaging;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.App.Avalonia.Messages;
using Wabbajack.App.Avalonia.Util;
using Wabbajack.Common;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Installer;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using Wabbajack.Services.OSIntegrated;

namespace Wabbajack.App.Avalonia.ViewModels.Installer;

public enum InstallState { Configuration, Installing, Success, Failure }

public class InstallationVM : ViewModelBase
{
    private const string LastLoadedModlist = "last-loaded-modlist";
    private const string InstallSettingsPrefix = "install-settings-";

    [Reactive] public ModList? ModList { get; set; }
    [Reactive] public ModlistMetadata? ModlistMetadata { get; set; }
    [Reactive] public AbsolutePath WabbajackFilePath { get; set; }
    [Reactive] public MO2InstallerVM Installer { get; set; }
    [Reactive] public Bitmap? ModListImage { get; set; }
    [Reactive] public InstallState InstallState { get; set; }
    [Reactive] public string ProgressText { get; set; } = "";
    [Reactive] public Percent ProgressPercent { get; set; }
    [Reactive] public string HashingSpeed { get; set; } = "";
    [Reactive] public string ExtractingSpeed { get; set; } = "";
    [Reactive] public string DownloadingSpeed { get; set; } = "";

    // Avoid [Reactive] on nullable enum — causes InvalidProgramException in DI
    private InstallResult? _installResult;
    public InstallResult? InstallResult
    {
        get => _installResult;
        set => this.RaiseAndSetIfChanged(ref _installResult, value);
    }

    private StandardInstaller? _standardInstaller;
    private CancellationTokenSource? _cancellationTokenSource;

    private readonly ILogger<InstallationVM> _logger;
    private readonly DTOSerializer _dtos;
    private readonly SettingsManager _settingsManager;
    private readonly IServiceProvider _serviceProvider;
    private readonly IGameLocator _gameLocator;
    private readonly Wabbajack.Services.OSIntegrated.Configuration _configuration;

    public ICommand InstallCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand BackToGalleryCommand { get; }
    public ICommand EditInstallDetailsCommand { get; }
    public ICommand OpenReadmeCommand { get; }
    public ICommand OpenWebsiteCommand { get; }
    public ICommand OpenLogFolderCommand { get; }
    public ICommand OpenInstallFolderCommand { get; }
    public ICommand CreateShortcutCommand { get; }

    public InstallationVM(
        ILogger<InstallationVM> logger,
        DTOSerializer dtos,
        SettingsManager settingsManager,
        IServiceProvider serviceProvider,
        IGameLocator gameLocator,
        Wabbajack.Services.OSIntegrated.Configuration configuration)
    {
        _logger = logger;
        _dtos = dtos;
        _settingsManager = settingsManager;
        _serviceProvider = serviceProvider;
        _gameLocator = gameLocator;
        _configuration = configuration;

        Installer = new MO2InstallerVM();

        var canInstall = this.WhenAnyValue(
            vm => vm.LoadingLock.IsNotLoading,
            vm => vm.Installer.InstallPath,
            vm => vm.Installer.DownloadPath,
            (notLoading, install, download) =>
                notLoading &&
                !string.IsNullOrWhiteSpace(install) &&
                !string.IsNullOrWhiteSpace(download));

        InstallCommand = ReactiveCommand.Create(
            () => BeginInstall().FireAndForget(),
            canInstall);

        CancelCommand = ReactiveCommand.Create(
            CancelInstall,
            this.WhenAnyValue(vm => vm.LoadingLock.IsNotLoading));

        BackToGalleryCommand = ReactiveCommand.Create(
            () => NavigateToGlobal.Send(ScreenType.ModListGallery));

        EditInstallDetailsCommand = ReactiveCommand.Create(() =>
        {
            InstallState = InstallState.Configuration;
            ProgressText = "";
        });

        OpenReadmeCommand = ReactiveCommand.Create(() =>
        {
            if (!string.IsNullOrWhiteSpace(ModList?.Readme))
                UIUtils.OpenWebsite(ModList.Readme);
        });

        OpenWebsiteCommand = ReactiveCommand.Create(() =>
        {
            if (!string.IsNullOrWhiteSpace(ModList?.Website))
                UIUtils.OpenWebsite(ModList.Website);
        });

        OpenLogFolderCommand = ReactiveCommand.Create(() =>
            UIUtils.OpenFolder(_configuration.LogLocation));

        OpenInstallFolderCommand = ReactiveCommand.Create(() =>
        {
            if (!string.IsNullOrWhiteSpace(Installer.InstallPath))
                UIUtils.OpenFolder((AbsolutePath)Installer.InstallPath);
        });

        CreateShortcutCommand = ReactiveCommand.Create(CreateDesktopShortcut);

        MessageBus.Current.Listen<LoadModlistForInstalling>()
            .Subscribe(msg => LoadModlistFromMessage(msg.Path, msg.Metadata).FireAndForget())
            .DisposeWith(CompositeDisposable);

        this.WhenActivated(disposables =>
        {
            // Auto-set download path when install path is chosen
            this.WhenAnyValue(vm => vm.Installer.InstallPath)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Subscribe(p =>
                {
                    if (string.IsNullOrWhiteSpace(Installer.DownloadPath))
                        Installer.DownloadPath = p.TrimEnd('\\', '/') + "\\downloads";
                })
                .DisposeWith(disposables);
        });
    }

    private async Task LoadModlistFromMessage(AbsolutePath path, ModlistMetadata metadata)
    {
        WabbajackFilePath = path;
        ModlistMetadata = metadata;
        await LoadModlist(path, metadata);
    }

    private async Task LoadModlist(AbsolutePath path, ModlistMetadata? metadata)
    {
        using var ll = LoadingLock.WithLoading();
        InstallState = InstallState.Configuration;
        try
        {
            ModList = await StandardInstaller.LoadFromFile(_dtos, path);

            var stream = await StandardInstaller.ModListImageStream(path);
            if (stream != null)
            {
                ModListImage = new Bitmap(stream);
                await stream.DisposeAsync();
            }

            // Load previous install settings for this modlist
            var settingsKey = InstallSettingsPrefix + path.FileName;
            var prevSettings = await _settingsManager.Load<SavedInstallSettings>(settingsKey);
            if (prevSettings?.ModListLocation.FileName == path.FileName &&
                !string.IsNullOrEmpty(prevSettings.InstallLocation.ToString()))
            {
                Installer.InstallPath = prevSettings.InstallLocation.ToString();
                Installer.DownloadPath = prevSettings.DownloadLocation.ToString();
            }

            ll.Succeed();
            await _settingsManager.Save(LastLoadedModlist, path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "While loading modlist");
            ll.Fail();
            ProgressText = "Failed to load modlist";
        }
    }

    private async Task BeginInstall()
    {
        await Task.Run(async () =>
        {
            RxApp.MainThreadScheduler.Schedule(() =>
            {
                ProgressText = $"Installing {ModList?.Name}…";
                InstallState = InstallState.Installing;
            });

            var settingsKey = InstallSettingsPrefix + WabbajackFilePath.FileName;
            await _settingsManager.Save(settingsKey, new SavedInstallSettings
            {
                ModListLocation = WabbajackFilePath,
                InstallLocation = (AbsolutePath)Installer.InstallPath,
                DownloadLocation = (AbsolutePath)Installer.DownloadPath,
                Metadata = ModlistMetadata
            });
            await _settingsManager.Save(LastLoadedModlist, WabbajackFilePath);

            try
            {
                var freshModList = await StandardInstaller.LoadFromFile(_dtos, WabbajackFilePath);

                _gameLocator.TryFindLocation(freshModList.GameType, out var gameFolder);

                var cfg = new InstallerConfiguration
                {
                    Game = freshModList.GameType,
                    OtherGames = Array.Empty<Game>(),
                    Downloads = (AbsolutePath)Installer.DownloadPath,
                    Install = (AbsolutePath)Installer.InstallPath,
                    ModList = freshModList,
                    ModlistArchive = WabbajackFilePath,
                    SystemParameters = null,
                    GameFolder = gameFolder,
                    Metadata = ModlistMetadata
                };

                _standardInstaller = StandardInstaller.Create(_serviceProvider, cfg);
                _standardInstaller.OnStatusUpdate = update =>
                {
                    RxApp.MainThreadScheduler.Schedule(() =>
                    {
                        ProgressText = update.StatusText;
                        ProgressPercent = update.StepsProgress;
                    });
                };

                _logger.LogInformation("Starting installation of {Name} to {Install}", cfg.ModList.Name, cfg.Install);

                Wabbajack.Installer.InstallResult result;
                using (_cancellationTokenSource = new CancellationTokenSource())
                {
                    result = await _standardInstaller.Begin(_cancellationTokenSource.Token);
                }

                RxApp.MainThreadScheduler.Schedule(() =>
                {
                    InstallResult = result;
                    if (result == Wabbajack.Installer.InstallResult.Succeeded)
                    {
                        ProgressText = $"Finished installing {ModList?.Name}";
                        InstallState = InstallState.Success;
                    }
                    else
                    {
                        InstallState = InstallState.Failure;
                        ProgressText = $"Error during installation of {ModList?.Name}";
                        ProgressPercent = Percent.Zero;
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "During installation");
                RxApp.MainThreadScheduler.Schedule(() =>
                {
                    InstallState = InstallState.Failure;
                    ProgressText = $"Error during installation of {ModList?.Name}";
                    ProgressPercent = Percent.Zero;
                    InstallResult = Wabbajack.Installer.InstallResult.Errored;
                });
            }
        });
    }

    private void CancelInstall()
    {
        switch (InstallState)
        {
            case InstallState.Configuration:
                NavigateToGlobal.Send(ScreenType.ModListGallery);
                break;
            case InstallState.Installing:
                if (_cancellationTokenSource is { IsCancellationRequested: false })
                    _cancellationTokenSource.CancelAsync().FireAndForget();
                break;
        }
    }

    private void CreateDesktopShortcut()
    {
        if (string.IsNullOrEmpty(Installer.InstallPath) || ModList == null) return;
        var deskDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var moPath = Path.Combine(Installer.InstallPath, "ModOrganizer.exe").Replace('\\', '/');
        using var writer = new StreamWriter(Path.Combine(deskDir, ModList.Name + ".url"));
        writer.WriteLine("[InternetShortcut]");
        writer.WriteLine("URL=file:///" + moPath);
        writer.WriteLine("IconIndex=0");
        writer.WriteLine("IconFile=" + moPath);
    }

    private class SavedInstallSettings
    {
        public AbsolutePath ModListLocation { get; set; }
        public AbsolutePath InstallLocation { get; set; }
        public AbsolutePath DownloadLocation { get; set; }
        public ModlistMetadata? Metadata { get; set; }
    }
}
