using System;
using ReactiveUI;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Media.Imaging;
using ReactiveUI.Fody.Helpers;
using System.Windows.Media;
using DynamicData;
using DynamicData.Binding;
using System.Reactive;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Shell;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAPICodePack.Dialogs;
using Microsoft.WindowsAPICodePack.Shell;
using Wabbajack.Common;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Extensions;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Installer;
using Wabbajack.Interventions;
using Wabbajack.Messages;
using Wabbajack.Models;
using Wabbajack.Paths;
using Wabbajack.RateLimiter;
using Wabbajack.View_Models;
using Wabbajack.Paths.IO;
using Wabbajack.Services.OSIntegrated;
using Wabbajack.Util;
using Consts = Wabbajack.Consts;
using KnownFolders = Wabbajack.Paths.IO.KnownFolders;

namespace Wabbajack;

public enum ModManager
{
    Standard
}

public enum InstallState
{
    Configuration,
    Installing,
    Success,
    Failure
}

public class InstallerVM : BackNavigatingVM, IBackNavigatingVM
{
    private const string LastLoadedModlist = "last-loaded-modlist";
    private const string InstallSettingsPrefix = "install-settings-";
    
    
    [Reactive]
    public Percent StatusProgress { get; set; }

    [Reactive]
    public string StatusText { get; set; }
    
    [Reactive]
    public ModList ModList { get; set; }
    
    [Reactive]
    public ModlistMetadata ModlistMetadata { get; set; }
    
    [Reactive]
    public ErrorResponse? Completed { get; set; }

    [Reactive]
    public FilePickerVM ModListLocation { get; set; }
    
    [Reactive]
    public MO2InstallerVM Installer { get; set; }
    
    [Reactive]
    public BitmapFrame ModListImage { get; set; }
    
    [Reactive]
    
    public BitmapFrame SlideShowImage { get; set; }


    [Reactive]
    public InstallState InstallState { get; set; }
    
    /// <summary>
    ///  Slideshow Data
    /// </summary>
    [Reactive]
    public string SlideShowTitle { get; set; }
    
    [Reactive]
    public string SlideShowAuthor { get; set; }
    
    [Reactive]
    public string SlideShowDescription { get; set; }


    private readonly ObservableAsPropertyHelper<bool> _installing;
    private readonly DTOSerializer _dtos;
    private readonly ILogger<InstallerVM> _logger;
    private readonly SettingsManager _settingsManager;
    private readonly IServiceProvider _serviceProvider;
    private readonly SystemParametersConstructor _parametersConstructor;
    private readonly IGameLocator _gameLocator;
    private readonly LoggerProvider _loggerProvider;

    [Reactive]
    public bool Installing { get; set; }
    
    [Reactive]
    public LoggerProvider LoggerProvider { get; set; }
    
    
    // Command properties
    public ReactiveCommand<Unit, Unit> ShowManifestCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenReadmeCommand { get; }
    public ReactiveCommand<Unit, Unit> VisitModListWebsiteCommand { get; }
        
    public ReactiveCommand<Unit, Unit> CloseWhenCompleteCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenLogsCommand { get; }
    public ReactiveCommand<Unit, Unit> GoToInstallCommand { get; }
    public ReactiveCommand<Unit, Unit> BeginCommand { get; }
    
    public ReactiveCommand<Unit, Unit> BackCommand { get; }

    public InstallerVM(ILogger<InstallerVM> logger, DTOSerializer dtos, SettingsManager settingsManager, IServiceProvider serviceProvider,
        SystemParametersConstructor parametersConstructor, IGameLocator gameLocator, LoggerProvider loggerProvider) : base(logger)
    {
        _logger = logger;
        LoggerProvider = loggerProvider;
        _settingsManager = settingsManager;
        _dtos = dtos;
        _serviceProvider = serviceProvider;
        _parametersConstructor = parametersConstructor;
        _gameLocator = gameLocator;

        Installer = new MO2InstallerVM(this);
        
        BackCommand = ReactiveCommand.Create(() => NavigateToGlobal.Send(NavigateToGlobal.ScreenType.ModeSelectionView));

        BeginCommand = ReactiveCommand.Create(() => BeginInstall().FireAndForget());
        
        OpenReadmeCommand = ReactiveCommand.Create(() =>
        {
            UIUtils.OpenWebsite(new Uri(ModList!.Readme));
        }, LoadingLock.IsNotLoadingObservable);
        
        VisitModListWebsiteCommand = ReactiveCommand.Create(() =>
        {
            UIUtils.OpenWebsite(ModList!.Website);
        }, LoadingLock.IsNotLoadingObservable);
        
        ModListLocation = new FilePickerVM
        {
            ExistCheckOption = FilePickerVM.CheckOptions.On,
            PathType = FilePickerVM.PathTypeOptions.File,
            PromptTitle = "Select a ModList to install"
        };
        ModListLocation.Filters.Add(new CommonFileDialogFilter("Wabbajack Modlist", "*.wabbajack"));
        
        MessageBus.Current.Listen<LoadModlistForInstalling>()
            .Subscribe(msg => LoadModlist(msg.Path, msg.Metadata).FireAndForget())
            .DisposeWith(CompositeDisposable);

        MessageBus.Current.Listen<LoadLastLoadedModlist>()
            .Subscribe(msg =>
            {
                LoadLastModlist().FireAndForget();
            });

        this.WhenActivated(disposables =>
        {

            
            ModListLocation.WhenAnyValue(l => l.TargetPath)
                .Subscribe(p => LoadModlist(p, null).FireAndForget())
                .DisposeWith(disposables);

        });

    }

    private async Task LoadLastModlist()
    {
        var lst = await _settingsManager.Load<AbsolutePath>(LastLoadedModlist);
        if (lst.FileExists())
            await LoadModlist(lst, null);
    }

    private async Task LoadModlist(AbsolutePath path, ModlistMetadata? metadata)
    {
        using var ll = LoadingLock.WithLoading();
        InstallState = InstallState.Configuration;
        ModListLocation.TargetPath = path;
        try
        {
            ModList = await StandardInstaller.LoadFromFile(_dtos, path);
            ModListImage = BitmapFrame.Create(await StandardInstaller.ModListImageStream(path));
            
            StatusText = $"Install configuration for {ModList.Name}";
            TaskBarUpdate.Send($"Loaded {ModList.Name}", TaskbarItemProgressState.Normal);
            
            var hex = (await ModListLocation.TargetPath.ToString().Hash()).ToHex();
            var prevSettings = await _settingsManager.Load<SavedInstallSettings>(InstallSettingsPrefix + hex);

            if (prevSettings.ModListLocation == path)
            {
                ModListLocation.TargetPath = prevSettings.ModListLocation;
                Installer.Location.TargetPath = prevSettings.InstallLocation;
                Installer.DownloadLocation.TargetPath = prevSettings.DownloadLoadction;
                ModlistMetadata = metadata ?? prevSettings.Metadata;
            }
            
            PopulateSlideShow(ModList);
            
            ll.Succeed();
            await _settingsManager.Save(LastLoadedModlist, path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "While loading modlist");
            ll.Fail();
        }
    }

    private async Task BeginInstall()
    {
        InstallState = InstallState.Installing;
        var postfix = (await ModListLocation.TargetPath.ToString().Hash()).ToHex();
        await _settingsManager.Save(InstallSettingsPrefix + postfix, new SavedInstallSettings
        {
            ModListLocation = ModListLocation.TargetPath,
            InstallLocation = Installer.Location.TargetPath,
            DownloadLoadction = Installer.DownloadLocation.TargetPath,
            Metadata = ModlistMetadata
        });

        try
        {
            var installer = StandardInstaller.Create(_serviceProvider, new InstallerConfiguration
            {
                Game = ModList.GameType,
                Downloads = Installer.DownloadLocation.TargetPath,
                Install = Installer.Location.TargetPath,
                ModList = ModList,
                ModlistArchive = ModListLocation.TargetPath,
                SystemParameters = _parametersConstructor.Create(),
                GameFolder = _gameLocator.GameLocation(ModList.GameType)
            });


            installer.OnStatusUpdate = update =>
            {
                StatusText = update.StatusText;
                StatusProgress = update.StepsProgress;

                TaskBarUpdate.Send(update.StatusText, TaskbarItemProgressState.Indeterminate, update.StepsProgress.Value);
            };
            await installer.Begin(CancellationToken.None);
            
            TaskBarUpdate.Send($"Finished install of {ModList.Name}", TaskbarItemProgressState.Normal);

            InstallState = InstallState.Success;
        }
        catch (Exception ex)
        {
            TaskBarUpdate.Send($"Error during install of {ModList.Name}", TaskbarItemProgressState.Error);
            InstallState = InstallState.Failure;
        }

    }


    class SavedInstallSettings
    {
        public AbsolutePath ModListLocation { get; set; }
        public AbsolutePath InstallLocation { get; set; }
        public AbsolutePath DownloadLoadction { get; set; }
        
        public ModlistMetadata Metadata { get; set; }
    }

    private void PopulateSlideShow(ModList modList)
    {
        SlideShowTitle = modList.Name;
        SlideShowAuthor = modList.Author;
        SlideShowDescription = modList.Description;
        SlideShowImage = ModListImage;
    }

}