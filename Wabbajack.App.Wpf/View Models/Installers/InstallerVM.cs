using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using ReactiveUI;
using System.Reactive.Disposables;
using System.Windows.Media.Imaging;
using ReactiveUI.Fody.Helpers;
using DynamicData;
using System.Reactive;
using System.Reactive.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Shell;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAPICodePack.Dialogs;
using Wabbajack.Common;
using Wabbajack.Downloaders;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Installer;
using Wabbajack.LoginManagers;
using Wabbajack.Messages;
using Wabbajack.Models;
using Wabbajack.Paths;
using Wabbajack.RateLimiter;
using Wabbajack.Paths.IO;
using Wabbajack.Services.OSIntegrated;
using Wabbajack.Util;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Wabbajack.CLI.Verbs;
using Wabbajack.VFS;

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

public class InstallerVM : BackNavigatingVM, IBackNavigatingVM, ICpuStatusVM
{
    private const string LastLoadedModlist = "last-loaded-modlist";
    private const string InstallSettingsPrefix = "install-settings-";
    private Random _random = new();
    
    
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
    
    [Reactive]
    protected ErrorResponse[] Errors { get; private set; }
    
    [Reactive]
    public ErrorResponse Error { get; private set; }

    /// <summary>
    ///  Slideshow Data
    /// </summary>
    [Reactive]
    public string SlideShowTitle { get; set; }
    
    [Reactive]
    public string SlideShowAuthor { get; set; }
    
    [Reactive]
    public string SlideShowDescription { get; set; }


    private readonly DTOSerializer _dtos;
    private readonly ILogger<InstallerVM> _logger;
    private readonly SettingsManager _settingsManager;
    private readonly IServiceProvider _serviceProvider;
    private readonly SystemParametersConstructor _parametersConstructor;
    private readonly IGameLocator _gameLocator;
    private readonly ResourceMonitor _resourceMonitor;
    private readonly Services.OSIntegrated.Configuration _configuration;
    private readonly HttpClient _client;
    private readonly DownloadDispatcher _downloadDispatcher;
    private readonly IEnumerable<INeedsLogin> _logins;
    private readonly CancellationToken _cancellationToken;
    public ReadOnlyObservableCollection<CPUDisplayVM> StatusList => _resourceMonitor.Tasks;

    [Reactive]
    public bool Installing { get; set; }
    
    [Reactive]
    public ErrorResponse ErrorState { get; set; }
    
    [Reactive]
    public bool ShowNSFWSlides { get; set; }
    
    public LogStream LoggerProvider { get; }

    private AbsolutePath LastInstallPath { get; set; }

    [Reactive] public bool OverwriteFiles { get; set; }
    
    
    // Command properties
    public ReactiveCommand<Unit, Unit> ShowManifestCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenReadmeCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenWikiCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenDiscordButton { get; }
    public ReactiveCommand<Unit, Unit> VisitModListWebsiteCommand { get; }
        
    public ReactiveCommand<Unit, Unit> CloseWhenCompleteCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenLogsCommand { get; }
    public ReactiveCommand<Unit, Unit> GoToInstallCommand { get; }
    public ReactiveCommand<Unit, Unit> BeginCommand { get; }
    
    public ReactiveCommand<Unit, Unit> VerifyCommand { get; }
    
    public InstallerVM(ILogger<InstallerVM> logger, DTOSerializer dtos, SettingsManager settingsManager, IServiceProvider serviceProvider,
        SystemParametersConstructor parametersConstructor, IGameLocator gameLocator, LogStream loggerProvider, ResourceMonitor resourceMonitor,
        Wabbajack.Services.OSIntegrated.Configuration configuration, HttpClient client, DownloadDispatcher dispatcher, IEnumerable<INeedsLogin> logins,
        CancellationToken cancellationToken) : base(logger)
    {
        _logger = logger;
        _configuration = configuration;
        LoggerProvider = loggerProvider;
        _settingsManager = settingsManager;
        _dtos = dtos;
        _serviceProvider = serviceProvider;
        _parametersConstructor = parametersConstructor;
        _gameLocator = gameLocator;
        _resourceMonitor = resourceMonitor;
        _client = client;
        _downloadDispatcher = dispatcher;
        _logins = logins;
        _cancellationToken = cancellationToken;

        Installer = new MO2InstallerVM(this);
        
        BackCommand = ReactiveCommand.Create(() => NavigateToGlobal.Send(NavigateToGlobal.ScreenType.ModeSelectionView));

        BeginCommand = ReactiveCommand.Create(() => BeginInstall().FireAndForget());
        
        VerifyCommand = ReactiveCommand.Create(() => Verify().FireAndForget());

        OpenReadmeCommand = ReactiveCommand.Create(() =>
        {
            UIUtils.OpenWebsite(new Uri(ModList!.Readme));
        }, this.WhenAnyValue(vm => vm.LoadingLock.IsNotLoading, vm => vm.ModList.Readme, (isNotLoading, readme) => isNotLoading && !string.IsNullOrWhiteSpace(readme)));

        OpenWikiCommand = ReactiveCommand.Create(() =>
        {
            UIUtils.OpenWebsite(new Uri("https://wiki.wabbajack.org/index.html"));
        }, this.WhenAnyValue(vm => vm.LoadingLock.IsNotLoading));

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
        
        OpenLogsCommand = ReactiveCommand.Create(() =>
        {
            UIUtils.OpenFolder(_configuration.LogLocation);
        });

        OpenDiscordButton = ReactiveCommand.Create(() =>
        {
            UIUtils.OpenWebsite(new Uri(ModlistMetadata.Links.DiscordURL));
        }, this.WhenAnyValue(x => x.ModlistMetadata)
            .WhereNotNull()
            .Select(md => !string.IsNullOrWhiteSpace(md.Links.DiscordURL)));
        
        ShowManifestCommand = ReactiveCommand.Create(() =>
        {
            UIUtils.OpenWebsite(new Uri("https://www.wabbajack.org/search/" + ModlistMetadata.NamespacedName));
        }, this.WhenAnyValue(x => x.ModlistMetadata)
            .WhereNotNull()
            .Select(md => !string.IsNullOrWhiteSpace(md.Links.MachineURL)));

        CloseWhenCompleteCommand = ReactiveCommand.Create(() =>
        {
            Environment.Exit(0);
        });
        
        GoToInstallCommand = ReactiveCommand.Create(() =>
        {
            UIUtils.OpenFolder(Installer.Location.TargetPath);
        });

        this.WhenAnyValue(x => x.OverwriteFiles)
            .Subscribe(x => ConfirmOverwrite());

        MessageBus.Current.Listen<LoadModlistForInstalling>()
            .Subscribe(msg => LoadModlistFromGallery(msg.Path, msg.Metadata).FireAndForget())
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

            var token = new CancellationTokenSource();
            BeginSlideShow(token.Token).FireAndForget();
            Disposable.Create(() => token.Cancel())
                .DisposeWith(disposables);
            
            this.WhenAny(vm => vm.ModListLocation.ErrorState)
                .CombineLatest(this.WhenAny(vm => vm.Installer.DownloadLocation.ErrorState),
                    this.WhenAny(vm => vm.Installer.Location.ErrorState),
                    this.WhenAny(vm => vm.ModListLocation.TargetPath),
                    this.WhenAny(vm => vm.Installer.Location.TargetPath),
                    this.WhenAny(vm => vm.Installer.DownloadLocation.TargetPath))
                .Select(t =>
                {
                    var errors = new[] {t.First, t.Second, t.Third}
                        .Where(t => t.Failed)
                        .Concat(Validate())
                        .ToArray();
                    if (!errors.Any()) return ErrorResponse.Success;
                    return ErrorResponse.Fail(string.Join("\n", errors.Select(e => e.Reason)));
                })
                .BindTo(this, vm => vm.ErrorState)
                .DisposeWith(disposables);
        });

    }

    private IEnumerable<ErrorResponse> Validate()
    {
        if (!ModListLocation.TargetPath.FileExists())
            yield return ErrorResponse.Fail("Mod list source does not exist");

        var downloadPath = Installer.DownloadLocation.TargetPath;
        if (downloadPath.Depth <= 1)
            yield return ErrorResponse.Fail("Download path isn't set to a folder");
        
        var installPath = Installer.Location.TargetPath;
        if (installPath.Depth <= 1)
            yield return ErrorResponse.Fail("Install path isn't set to a folder");
        if (installPath.InFolder(KnownFolders.Windows))
            yield return ErrorResponse.Fail("Don't install modlists into your Windows folder");
        if( installPath.ToString().Length > 0 && downloadPath.ToString().Length > 0 && installPath == downloadPath)
        {
            yield return ErrorResponse.Fail("Can't have identical install and download folders");
        }
        if (installPath.ToString().Length > 0 && downloadPath.ToString().Length > 0 && KnownFolders.IsSubDirectoryOf(installPath.ToString(), downloadPath.ToString()))
        {
            yield return ErrorResponse.Fail("Can't put the install folder inside the download folder");
        }
        foreach (var game in GameRegistry.Games)
        {
            if (!_gameLocator.TryFindLocation(game.Key, out var location))
                continue;
            
            if (installPath.InFolder(location))
                yield return ErrorResponse.Fail("Can't install a modlist into a game folder");

            if (location.ThisAndAllParents().Any(path => installPath == path))
            {
                yield return ErrorResponse.Fail(
                    "Can't install in this path, installed files may overwrite important game files");
            }
        }
        
        if (installPath.InFolder(KnownFolders.EntryPoint))
            yield return ErrorResponse.Fail("Can't install a modlist into the Wabbajack.exe path");
        if (downloadPath.InFolder(KnownFolders.EntryPoint))
            yield return ErrorResponse.Fail("Can't download a modlist into the Wabbajack.exe path");
        if (KnownFolders.EntryPoint.ThisAndAllParents().Any(path => installPath == path))
        { 
            yield return ErrorResponse.Fail("Installing in this folder may overwrite Wabbajack");
        }

        if (installPath.ToString().Length != 0 && installPath != LastInstallPath && !OverwriteFiles &&
            Directory.EnumerateFileSystemEntries(installPath.ToString()).Any())
        {
            yield return ErrorResponse.Fail("There are files in the install folder, please tick 'Overwrite Installation' to confirm you want to install to this folder " + Environment.NewLine + 
                 "if you are updating an existing modlist, then this is expected and can be overwritten.");
        }

        if (KnownFolders.IsInSpecialFolder(installPath) || KnownFolders.IsInSpecialFolder(downloadPath))
        {
            yield return ErrorResponse.Fail("Can't install into Windows locations such as Documents etc, please make a new folder for the modlist - C:\\ModList\\ for example.");
        }
        // Disabled Because it was causing issues for people trying to update lists.
        //if (installPath.ToString().Length > 0 && downloadPath.ToString().Length > 0 && !HasEnoughSpace(installPath, downloadPath)){
        //    yield return ErrorResponse.Fail("Can't install modlist due to lack of free hard drive space, please read the modlist Readme to learn more.");
        //}
    }
    
    /*
    private bool HasEnoughSpace(AbsolutePath inpath, AbsolutePath downpath)
    {      
        string driveLetterInPath = inpath.ToString().Substring(0,1);
        string driveLetterDownPath = inpath.ToString().Substring(0,1);
        DriveInfo driveUsedInPath = new DriveInfo(driveLetterInPath);
        DriveInfo driveUsedDownPath = new DriveInfo(driveLetterDownPath);
        long spaceRequiredforInstall = ModlistMetadata.DownloadMetadata.SizeOfInstalledFiles;
        long spaceRequiredforDownload = ModlistMetadata.DownloadMetadata.SizeOfArchives;
        long spaceInstRemaining = driveUsedInPath.AvailableFreeSpace;
        long spaceDownRemaining = driveUsedDownPath.AvailableFreeSpace;
        if ( driveLetterInPath == driveLetterDownPath)
        {
            long totalSpaceRequired = spaceRequiredforInstall + spaceRequiredforDownload;
            if (spaceInstRemaining < totalSpaceRequired)
            {
                return false;
            }

        } else
        {
            if( spaceInstRemaining < spaceRequiredforInstall || spaceDownRemaining < spaceRequiredforDownload)
            {
                return false;
            }
        }
        return true;

    }*/

    private async Task BeginSlideShow(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await Task.Delay(5000, token);
            if (InstallState == InstallState.Installing)
            {
                await PopulateNextModSlide(ModList);
            }
        }
    }

    private async Task LoadLastModlist()
    {
        var lst = await _settingsManager.Load<AbsolutePath>(LastLoadedModlist);
        if (lst.FileExists())
        {
            ModListLocation.TargetPath = lst;
        }
    }

    private async Task LoadModlistFromGallery(AbsolutePath path, ModlistMetadata metadata)
    {
        ModListLocation.TargetPath = path;
        ModlistMetadata = metadata;
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
            
            if (!string.IsNullOrWhiteSpace(ModList.Readme)) 
                UIUtils.OpenWebsite(new Uri(ModList.Readme));


            StatusText = $"Install configuration for {ModList.Name}";
            TaskBarUpdate.Send($"Loaded {ModList.Name}", TaskbarItemProgressState.Normal);
            
            var hex = (await ModListLocation.TargetPath.ToString().Hash()).ToHex();
            var prevSettings = await _settingsManager.Load<SavedInstallSettings>(InstallSettingsPrefix + hex);

            if (path.WithExtension(Ext.MetaData).FileExists())
            {
                try
                {
                    metadata = JsonSerializer.Deserialize<ModlistMetadata>(await path.WithExtension(Ext.MetaData)
                        .ReadAllTextAsync());
                    ModlistMetadata = metadata;
                }
                catch (Exception ex)
                {
                    _logger.LogInformation(ex, "Can't load metadata cached next to file");
                }
            }

            if (prevSettings.ModListLocation == path)
            {
                ModListLocation.TargetPath = prevSettings.ModListLocation;
                LastInstallPath = prevSettings.InstallLocation;
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

    private void ConfirmOverwrite()
    {
        AbsolutePath prev = Installer.Location.TargetPath;
        Installer.Location.TargetPath = "".ToAbsolutePath();
        Installer.Location.TargetPath = prev;
    }

    private async Task Verify()
    {
        await Task.Run(async () =>
        {
            InstallState = InstallState.Installing;
            
            StatusText = $"Verifying {ModList.Name}";
            
            
            var cmd = new VerifyModlistInstall(_serviceProvider.GetRequiredService<ILogger<VerifyModlistInstall>>(), _dtos, 
                _serviceProvider.GetRequiredService<IResource<FileHashCache>>(), 
                _serviceProvider.GetRequiredService<TemporaryFileManager>());
            
            var result = await cmd.Run(ModListLocation.TargetPath, Installer.Location.TargetPath, _cancellationToken);
            
            if (result != 0)
            {
                TaskBarUpdate.Send($"Error during verification of {ModList.Name}", TaskbarItemProgressState.Error);
                InstallState = InstallState.Failure;
                StatusText = $"Error during install of {ModList.Name}";
                StatusProgress = Percent.Zero;
            }
            else
            {
                TaskBarUpdate.Send($"Finished verification of {ModList.Name}", TaskbarItemProgressState.Normal);
                InstallState = InstallState.Success;
            }

        });
    }

    private async Task BeginInstall()
    {
        await Task.Run(async () =>
        {
            InstallState = InstallState.Installing;

            foreach (var downloader in await _downloadDispatcher.AllDownloaders(ModList.Archives.Select(a => a.State)))
            {
                _logger.LogInformation("Preparing {Name}", downloader.GetType().Name);
                if (await downloader.Prepare())
                    continue;

                var manager = _logins
                    .FirstOrDefault(l => l.LoginFor() == downloader.GetType());
                if (manager == null)
                {
                    _logger.LogError("Cannot install, could not prepare {Name} for downloading",
                        downloader.GetType().Name);
                    throw new Exception($"No way to prepare {downloader}");
                }

                RxApp.MainThreadScheduler.Schedule(manager, (_, _) =>
                {
                    manager.TriggerLogin.Execute(null);
                    return Disposable.Empty;
                });

                while (true)
                {
                    if (await downloader.Prepare())
                        break;
                    await Task.Delay(1000);
                }
            }
            
            
            var postfix = (await ModListLocation.TargetPath.ToString().Hash()).ToHex();
            await _settingsManager.Save(InstallSettingsPrefix + postfix, new SavedInstallSettings
            {
                ModListLocation = ModListLocation.TargetPath,
                InstallLocation = Installer.Location.TargetPath,
                DownloadLoadction = Installer.DownloadLocation.TargetPath,
                Metadata = ModlistMetadata
            });
            await _settingsManager.Save(LastLoadedModlist, ModListLocation.TargetPath);

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

                    TaskBarUpdate.Send(update.StatusText, TaskbarItemProgressState.Indeterminate,
                        update.StepsProgress.Value);
                };

                if (!await installer.Begin(_cancellationToken))
                {
                    TaskBarUpdate.Send($"Error during install of {ModList.Name}", TaskbarItemProgressState.Error);
                    InstallState = InstallState.Failure;
                    StatusText = $"Error during install of {ModList.Name}";
                    StatusProgress = Percent.Zero;
                }
                else
                {
                    TaskBarUpdate.Send($"Finished install of {ModList.Name}", TaskbarItemProgressState.Normal);
                    InstallState = InstallState.Success;
                    
                    if (!string.IsNullOrWhiteSpace(ModList.Readme)) 
                        UIUtils.OpenWebsite(new Uri(ModList.Readme));

                }
            }
            catch (Exception ex)
            {
                TaskBarUpdate.Send($"Error during install of {ModList.Name}", TaskbarItemProgressState.Error);
                _logger.LogError(ex, ex.Message);
                InstallState = InstallState.Failure;
                StatusText = $"Error during install of {ModList.Name}";
                StatusProgress = Percent.Zero;
            }
        });

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
        if (ModlistMetadata.ImageContainsTitle && ModlistMetadata.DisplayVersionOnlyInInstallerView)
        {
            SlideShowTitle = "v" + ModlistMetadata.Version.ToString();
        }
        else
        {
            SlideShowTitle = modList.Name;
        }
        SlideShowAuthor = modList.Author;
        SlideShowDescription = modList.Description;
        SlideShowImage = ModListImage;
    }


    private async Task PopulateNextModSlide(ModList modList)
    {
        try
        {
            var mods = modList.Archives.Select(a => a.State)
                .OfType<IMetaState>()
                .Where(t => ShowNSFWSlides || !t.IsNSFW)
                .Where(t => t.ImageURL != null)
                .ToArray();
            var thisMod = mods[_random.Next(0, mods.Length)];
            var data = await _client.GetByteArrayAsync(thisMod.ImageURL!);
            var image = BitmapFrame.Create(new MemoryStream(data));
            SlideShowTitle = thisMod.Name;
            SlideShowAuthor = thisMod.Author;
            SlideShowDescription = thisMod.Description;
            SlideShowImage = image;
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "While loading slide");
        }
    }

}
