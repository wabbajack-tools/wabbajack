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
using Wabbajack.CLI.Verbs;
using Microsoft.Extensions.DependencyInjection;
using Wabbajack.VFS;
using Humanizer;
using System.Text.RegularExpressions;
using System.Windows.Input;
using Microsoft.Web.WebView2.Wpf;
using System.Diagnostics;
using System.Reactive.Concurrency;

namespace Wabbajack;

public enum InstallState
{
    Configuration,
    Installing,
    Success,
    Failure
}

public class InstallationVM : ProgressViewModel, ICpuStatusVM
{
    private const string LastLoadedModlist = "last-loaded-modlist";
    private const string InstallSettingsPrefix = "install-settings-";
    private readonly Random _random = new();
    
    
    [Reactive] public ModList ModList { get; set; }
    [Reactive] public ModlistMetadata ModlistMetadata { get; set; }
    [Reactive] public FilePickerVM WabbajackFileLocation { get; set; }
    [Reactive] public MO2InstallerVM Installer { get; set; }
    [Reactive] public StandardInstaller StandardInstaller { get; set; }
    [Reactive] public BitmapImage ModListImage { get; set; }
    [Reactive] public InstallState InstallState { get; set; }

    /// <summary>
    /// Don't use the Reactive attribute on nullable enum values
    /// This causes InvalidProgramExceptions on requesting this service via DependencyInjection 
    /// </summary>
    private InstallResult? _installResult = null;
    public InstallResult? InstallResult
    {
        get => _installResult;
        set
        {
            RaiseAndSetIfChanged(ref _installResult, value);
            _installResult = value;
        }
    }

    /// <summary>
    ///  Slideshow Data
    /// </summary>
    [Reactive] public BitmapFrame SlideShowImage { get; set; }
    [Reactive] public string SlideShowTitle { get; set; } 
    [Reactive] public string SlideShowAuthor { get; set; }
    [Reactive] public string SlideShowDescription { get; set; }
    [Reactive] public string SuggestedInstallFolder { get; set; }
    [Reactive] public string SuggestedDownloadFolder { get; set; }

    public WebView2 ReadmeBrowser { get; set; }

    private readonly DTOSerializer _dtos;
    private readonly ILogger<InstallationVM> _logger;
    private readonly SettingsManager _settingsManager;
    private readonly IServiceProvider _serviceProvider;
    private readonly SystemParametersConstructor _parametersConstructor;
    private readonly IGameLocator _gameLocator;
    private readonly ResourceMonitor _resourceMonitor;
    private readonly Services.OSIntegrated.Configuration _configuration;
    private readonly HttpClient _client;
    private readonly DownloadDispatcher _downloadDispatcher;
    private readonly IEnumerable<INeedsLogin> _logins;
    private CancellationTokenSource _cancellationTokenSource;
    public ReadOnlyObservableCollection<CPUDisplayVM> StatusList => _resourceMonitor.Tasks;

    [Reactive] public bool Installing { get; set; }
    
    [Reactive] public ValidationResult ValidationResult { get; set; }
    
    [Reactive] public bool ShowNSFWSlides { get; set; }
    
    public LogStream LoggerProvider { get; }


    [Reactive] public string HashingSpeed { get; set; }
    [Reactive] public string ExtractingSpeed { get; set; }
    [Reactive] public string DownloadingSpeed { get; set; }
    
    // Command properties
    public ICommand OpenManifestCommand { get; }
    public ICommand OpenReadmeCommand { get; }
    public ICommand OpenWikiCommand { get; }
    public ICommand OpenDiscordButton { get; }
    public ICommand OpenWebsiteCommand { get; }
    public ICommand OpenMissingArchivesCommand { get; }
    public ICommand BackToGalleryCommand { get; }
    public ICommand OpenLogFolderCommand { get; }
    public ICommand OpenInstallFolderCommand { get; }
    public ICommand InstallCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand EditInstallDetailsCommand { get; }
    public ICommand VerifyCommand { get; }
    public ICommand CreateShortcutCommand { get; }
    
    public InstallationVM(ILogger<InstallationVM> logger, DTOSerializer dtos, SettingsManager settingsManager, IServiceProvider serviceProvider,
        SystemParametersConstructor parametersConstructor, IGameLocator gameLocator, LogStream loggerProvider, ResourceMonitor resourceMonitor,
        Services.OSIntegrated.Configuration configuration, HttpClient client, DownloadDispatcher dispatcher, IEnumerable<INeedsLogin> logins)
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

        ConfigurationText = $"Loading... Please wait";
        ProgressText = $"Installation";

        Installer = new MO2InstallerVM(this);
        ReadmeBrowser = serviceProvider.GetRequiredService<WebView2>();

        CancelCommand = ReactiveCommand.Create(CancelInstall, this.WhenAnyValue(vm => vm.LoadingLock.IsNotLoading));
        EditInstallDetailsCommand = ReactiveCommand.Create(() =>
        {
            ConfigurationText = "Preparation";
            ProgressText = $"Installation";
            CurrentStep = Step.Configuration;
            InstallState = InstallState.Configuration;
            ProgressState = ProgressState.Normal;
            this.Activator.Activate();
        });
        InstallCommand = ReactiveCommand.Create(() => BeginInstall().FireAndForget(), this.WhenAnyValue(vm => vm.LoadingLock.IsNotLoading,
                                                                                                        vm => vm.ValidationResult,
                                                                                                        (notLoading, validationResult) => notLoading && (validationResult?.Succeeded ?? true)));

        OpenReadmeCommand = ReactiveCommand.Create(() =>
        {
            UIUtils.OpenWebsite(ModList!.Readme);
        }, this.WhenAnyValue(vm => vm.LoadingLock.IsNotLoading, vm => vm.ModList.Readme, (isNotLoading, readme) => isNotLoading && !string.IsNullOrWhiteSpace(readme)));

        OpenWebsiteCommand = ReactiveCommand.Create(() =>
        {
            UIUtils.OpenWebsite(ModlistMetadata.Links.WebsiteURL);
        }, this.WhenAnyValue(vm => vm.LoadingLock.IsNotLoading, vm => vm.ModlistMetadata,
        (isNotLoading, metadata) => isNotLoading && !string.IsNullOrWhiteSpace(metadata?.Links.WebsiteURL)));
        
        WabbajackFileLocation = new FilePickerVM
        {
            ExistCheckOption = FilePickerVM.CheckOptions.On,
            PathType = FilePickerVM.PathTypeOptions.File,
            PromptTitle = "Select a modlist to install"
        };
        WabbajackFileLocation.Filters.Add(new CommonFileDialogFilter("Wabbajack modlist", "*.wabbajack"));
        
        OpenLogFolderCommand = ReactiveCommand.Create(() =>
        {
            UIUtils.OpenFolderAndSelectFile(_configuration.LogLocation.Combine("Wabbajack.current.log"));
        });

        OpenDiscordButton = ReactiveCommand.Create(() =>
        {
            UIUtils.OpenWebsite(new Uri(ModlistMetadata.Links.DiscordURL));
        }, this.WhenAnyValue(vm => vm.LoadingLock.IsNotLoading, vm => vm.ModlistMetadata,
        (isNotLoading, metadata) => isNotLoading && !string.IsNullOrEmpty(metadata?.Links?.DiscordURL)));

        OpenManifestCommand = ReactiveCommand.Create(() =>
        {
            // TODO: Open modlist archives in modal dialog
            UIUtils.OpenWebsite(new Uri("https://www.wabbajack.org/search/" + ModlistMetadata.NamespacedName));
        }, this.WhenAnyValue(x => x.LoadingLock.IsNotLoading, vm => vm.ModlistMetadata, (isNotLoading, metadata) => isNotLoading && !string.IsNullOrEmpty(metadata?.NamespacedName)));
        
        OpenInstallFolderCommand = ReactiveCommand.Create(() =>
        {
            UIUtils.OpenFolderAndSelectFile(Installer.Location.TargetPath.Combine("ModOrganizer.exe"));
        });

        OpenMissingArchivesCommand = ReactiveCommand.Create(() =>
        {
            var missing = ModList.Archives.Where(a => !StandardInstaller.HashedArchives.ContainsKey(a.Hash)).ToArray();
            ShowMissingManualReport(missing);
        });

        BackToGalleryCommand = ReactiveCommand.Create(() => NavigateToGlobal.Send(ScreenType.ModListGallery));

        CreateShortcutCommand = ReactiveCommand.Create(() => CreateDesktopShortcut());

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

            WabbajackFileLocation.WhenAnyValue(l => l.TargetPath)
                .Subscribe(p => LoadModlist(p, null).FireAndForget())
                .DisposeWith(disposables);

            _resourceMonitor.Updates
                .Subscribe(updates =>
                {
                    foreach (var update in updates)
                    {
                        switch (update.Name)
                        {
                            case "Downloads":
                                DownloadingSpeed = $"{update.Throughput.ToFileSizeString()}/s";
                                break;
                            case "File Hashing":
                                HashingSpeed = $"{update.Throughput.ToFileSizeString()}/s";
                                break;
                            case "File Extractor":
                                ExtractingSpeed = $"{update.Throughput.ToFileSizeString()}/s";
                                break;
                        }
                    }
                })
                .DisposeWith(disposables);

            /*
            var token = new CancellationTokenSource();
            BeginSlideShow(token.Token).FireAndForget();
            Disposable.Create(() => token.Cancel())
                .DisposeWith(disposables);
            */
            
            this.WhenAny(vm => vm.WabbajackFileLocation.ValidationResult)
                .CombineLatest<ValidationResult, ValidationResult, ValidationResult, AbsolutePath, AbsolutePath, AbsolutePath>(this.WhenAny(vm => vm.Installer.DownloadLocation.ValidationResult),
                    this.WhenAny(vm => vm.Installer.Location.ValidationResult),
                    this.WhenAny(vm => vm.WabbajackFileLocation.TargetPath),
                    this.WhenAny(vm => vm.Installer.Location.TargetPath),
                    this.WhenAny(vm => vm.Installer.DownloadLocation.TargetPath))
                .Select(t =>
                {
                    var errors = (new[] { t.First, t.Second, t.Third})
                        .Where(t => t.Failed)
                        .Concat(Validate())
                        .ToArray();
                    if (!errors.Any()) return ValidationResult.Success;
                    return ValidationResult.Fail(string.Join("\n", errors.Select(e => e.Reason)));
                })
                .BindTo(this, vm => vm.ValidationResult)
                .DisposeWith(disposables);

            this.WhenAny(vm => vm.InstallState)
                .Subscribe(state =>
                    {
                        CurrentStep = state switch
                        {
                            InstallState.Configuration => Step.Configuration,
                            InstallState.Installing => Step.Busy,
                            InstallState.Failure => Step.Configuration,
                            InstallState.Success => Step.Done,
                            _ => Step.Configuration
                        };
                        ProgressState = state switch
                        {
                            InstallState.Success => ProgressState.Success,
                            InstallState.Failure => ProgressState.Error,
                            _ => ProgressState.Normal
                        };
                    })
                .DisposeWith(disposables);

            this.WhenAnyValue(vm => vm.Installer.Location.TargetPath)
                .Select(x => x.PathParts.Any() ? x.Combine("downloads") : x)
                .Subscribe(x => Installer.DownloadLocation.TargetPath = x)
                .DisposeWith(disposables);
        });

    }

    private void CreateDesktopShortcut()
    {
        string deskDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

        using StreamWriter writer = new StreamWriter(deskDir + "\\" + ModList.Name + ".url");

        string path = Installer.Location.TargetPath.Combine("ModOrganizer.exe").ToString();
        writer.WriteLine("[InternetShortcut]");
        writer.WriteLine("URL=file:///" + path);
        writer.WriteLine("IconIndex=0");
        string icon = path.Replace('\\', '/');
        writer.WriteLine("IconFile=" + icon);
    }

    private static string GetSuggestedInstallFolder(ModlistMetadata x)
    {
        var folderName = x.Title;
        // Ignore everything after a dash
        folderName = folderName.Split('-')[0];
        // Remove all special characters
        folderName = Regex.Replace(folderName, "[^a-zA-Z0-9_ .]+", "");
        // Get preferred installation drive (SSD with enough space)
        var preferredPartition = DriveHelper.GetPreferredInstallationDrive(x.DownloadMetadata.SizeOfInstalledFiles);
        var words = folderName.Split(' ');
        // Abbreviate the list name if it's too long, otherwise convert it to PascalCase
        folderName = words.Length >= 3 ? string.Join("", words.Select(w => w[0])).ToUpper() : folderName.Pascalize();

        return $"{preferredPartition.Name}Modlists\\{folderName.Trim()}\\";
    }

    private async void CancelInstall()
    {
        switch(InstallState)
        {
            case InstallState.Configuration:
                NavigateToGlobal.Send(ScreenType.ModListGallery);
                break;

            case InstallState.Installing:
                // TODO - Cancel installation
                if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
                {
                    _logger.LogInformation("Cancellation was requested, cancelling installation of modlist!");
                    try
                    {
                        await _cancellationTokenSource.CancelAsync();
                    }
                    catch(ObjectDisposedException ex)
                    {
                        _logger.LogError("Token source was already disposed while attempting cancellation! Exception: {ex}", ex.ToString());
                    }
                }

                break;

            default:
                break;
        }
    }

    private IEnumerable<ValidationResult> Validate()
    {
        if (!WabbajackFileLocation.TargetPath.FileExists())
            yield return ValidationResult.Fail("Wabbajack modlist file does not exist");

        var downloadPath = Installer.DownloadLocation.TargetPath;
        if (downloadPath.Depth <= 1)
            yield return DownloadsPathValidationResult.Fail("Please specify a download location");
        
        var installPath = Installer.Location.TargetPath;
        if (installPath.Depth <= 1)
            yield return InstallPathValidationResult.Fail("Please specify an installation location");
        if (installPath.InFolder(KnownFolders.Windows))
            yield return InstallPathValidationResult.Fail("Can't install modlist to Windows folder");

        if (installPath.ToString().Length > 0 && downloadPath.ToString().Length > 0 && installPath == downloadPath)
        {
            yield return DownloadsPathValidationResult.Fail("Installation and download locations cannot be identical");
        }
        if (installPath.ToString().Length > 0 && downloadPath.ToString().Length > 0 && KnownFolders.IsSubDirectoryOf(installPath.ToString(), downloadPath.ToString()))
        {
            yield return InstallPathValidationResult.Fail("Can't install to folder within downloads folder");
        }
        foreach (var game in GameRegistry.Games)
        {
            if (!_gameLocator.TryFindLocation(game.Key, out var location))
                continue;
            
            if (installPath.InFolder(location))
                yield return InstallPathValidationResult.Fail("Can't install modlist into a game folder");

            if (location.ThisAndAllParents().Any(path => installPath == path))
            {
                yield return InstallPathValidationResult.Fail(
                    "Can't install to path, installed files may overwrite game files");
            }
        }
        
        if (installPath.InFolder(KnownFolders.EntryPoint))
            yield return InstallPathValidationResult.Fail("Can't install a modlist into the Wabbajack folder");
        if (downloadPath.InFolder(KnownFolders.EntryPoint))
            yield return DownloadsPathValidationResult.Fail("Can't download a modlist into the Wabbajack folder");
        if (KnownFolders.EntryPoint.ThisAndAllParents().Any(path => installPath == path))
        { 
            yield return InstallPathValidationResult.Fail("Can't install into the Wabbajack folder");
        }

        if (KnownFolders.IsInSpecialFolder(installPath, out var specialFolder) )
        {
            yield return InstallPathValidationResult.Fail($"Can't install into special folder ({specialFolder})");
        }
        if(KnownFolders.IsInSpecialFolder(downloadPath, out var specialDownloadsFolder))
        {
            yield return DownloadsPathValidationResult.Fail($"Can't download into special folder ({specialDownloadsFolder})");
        }
        // Disabled Because it was causing issues for people trying to update lists.
        //if (installPath.ToString().Length > 0 && downloadPath.ToString().Length > 0 && !HasEnoughSpace(installPath, downloadPath)){
        //    yield return InstallResponse.Fail("Can't install modlist due to lack of free hard drive space, please read the modlist Readme to learn more.");
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
            WabbajackFileLocation.TargetPath = lst;
        }
    }

    private async Task LoadModlistFromGallery(AbsolutePath path, ModlistMetadata metadata)
    {
        WabbajackFileLocation.TargetPath = path;
        ModlistMetadata = metadata;
    }

    private async Task LoadModlist(AbsolutePath path, ModlistMetadata? metadata)
    {
        using var ll = LoadingLock.WithLoading();
        InstallState = InstallState.Configuration;
        WabbajackFileLocation.TargetPath = path;
        try
        {
            ModList = await StandardInstaller.LoadFromFile(_dtos, path);
            var stream = await StandardInstaller.ModListImageStream(path);
            if(stream != null) ModListImage = UIUtils.BitmapImageFromStream(stream);

            ConfigurationText = $"Preparing to install {metadata?.Title ?? ModList.Name}";
            ProgressText = $"Installation";
            
            var hex = (await WabbajackFileLocation.TargetPath.FileName.ToString().Hash()).ToHex();
            var prevSettings = await _settingsManager.Load<SavedInstallSettings>(InstallSettingsPrefix + hex);
            bool hasPrevModListInstallation = !string.IsNullOrEmpty(prevSettings?.ModListLocation.ToString()) && prevSettings.ModListLocation.FileName == path.FileName;

            if(!hasPrevModListInstallation)
            {
                // Wabbajack combined this with path before 4.0, now only file name is considered
                hex = (await WabbajackFileLocation.TargetPath.ToString().Hash()).ToHex();
                prevSettings = await _settingsManager.Load<SavedInstallSettings>(InstallSettingsPrefix + hex);
                hasPrevModListInstallation = !string.IsNullOrEmpty(prevSettings?.ModListLocation.ToString()) && prevSettings.ModListLocation.FileName == path.FileName;
            }

            if (path.WithExtension(Ext.MetaData).FileExists())
            {
                try
                {
                    metadata = JsonSerializer.Deserialize<ModlistMetadata>(await path.WithExtension(Ext.MetaData)
                        .ReadAllTextAsync());
                    ModlistMetadata = metadata;
                    if (!hasPrevModListInstallation)
                    {
                        SuggestedInstallFolder = GetSuggestedInstallFolder(metadata);
                        SuggestedDownloadFolder = SuggestedInstallFolder + "\\downloads";
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogInformation(ex, "Can't load metadata next to file");
                }
            }
            else
            {
                _logger.LogInformation("Modlist metadata not loaded, possibly using local install without metadata file");
            }

            if (hasPrevModListInstallation)
            {
                Installer.Location.TargetPath = prevSettings.InstallLocation;
                Installer.DownloadLocation.TargetPath = prevSettings.DownloadLocation;
            }
            
            PopulateSlideShow(ModList);
            
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

    private async Task Verify()
    {
        await Task.Run(async () =>
        {
            InstallState = InstallState.Installing;

            ProgressText = $"Verifying {ModList.Name}";


            var cmd = new VerifyModlistInstall(_serviceProvider.GetRequiredService<ILogger<VerifyModlistInstall>>(), _dtos,
                _serviceProvider.GetRequiredService<IResource<FileHashCache>>(),
                _serviceProvider.GetRequiredService<TemporaryFileManager>());

            var result = await cmd.Run(WabbajackFileLocation.TargetPath, Installer.Location.TargetPath, _cancellationTokenSource.Token);

            if (result != 0)
            {
                TaskBarUpdate.Send($"Error during verification of {ModList.Name}", TaskbarItemProgressState.Error);
                InstallState = InstallState.Failure;
                ProgressText = $"Error during install of {ModList.Name}";
                ProgressPercent = Percent.Zero;
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
            RxApp.MainThreadScheduler.Schedule(() =>
            {
                ConfigurationText = "Preparation";
                ProgressText = $"Installing {ModList.Name}";
                CurrentStep = Step.Busy;
                InstallState = InstallState.Installing;
                ProgressState = ProgressState.Normal;
            });

            await PrepareDownloaders();

            var postfix = (await WabbajackFileLocation.TargetPath.FileName.ToString().Hash()).ToHex();
            await _settingsManager.Save(InstallSettingsPrefix + postfix, new SavedInstallSettings
            {
                ModListLocation = WabbajackFileLocation.TargetPath,
                InstallLocation = Installer.Location.TargetPath,
                DownloadLocation = Installer.DownloadLocation.TargetPath,
                Metadata = ModlistMetadata
            });
            await _settingsManager.Save(LastLoadedModlist, WabbajackFileLocation.TargetPath);

            try
            {
                var cfg = new InstallerConfiguration
                {
                    Game = ModList.GameType,
                    Downloads = Installer.DownloadLocation.TargetPath,
                    Install = Installer.Location.TargetPath,
                    ModList = ModList,
                    ModlistArchive = WabbajackFileLocation.TargetPath,
                    SystemParameters = _parametersConstructor.Create(),
                    GameFolder = _gameLocator.GameLocation(ModList.GameType)
                };
                StandardInstaller = StandardInstaller.Create(_serviceProvider, cfg);


                StandardInstaller.OnStatusUpdate = update =>
                {
                    RxApp.MainThreadScheduler.Schedule(() =>
                    {
                        ProgressText = update.StatusText;
                        ProgressPercent = update.StepsProgress;
                    });
                };

                _logger.LogInformation("Starting installation of {modlist} {version}:", cfg.ModList.Name, cfg.ModList.Version?.ToString() ?? "");
                _logger.LogInformation("    Installation folder: {installFolder}", cfg.Install.ToString());
                _logger.LogInformation("    Downloads folder: {downloadsFolder}", cfg.Downloads.ToString());
                _logger.LogInformation("    Modlist file location: {wjFileLocation}", cfg.ModlistArchive.ToString());
                _logger.LogInformation("    Game: {game}", cfg.Game.ToString());
                _logger.LogInformation("    Game folder: {gameFolder}", cfg.GameFolder);

                InstallResult result;
                using (_cancellationTokenSource = new CancellationTokenSource())
                {
                    result = await StandardInstaller.Begin(_cancellationTokenSource.Token);
                }
                if (result == Wabbajack.Installer.InstallResult.Succeeded)
                {
                    RxApp.MainThreadScheduler.Schedule(() =>
                    {
                        InstallResult = result;
                        ProgressText = $"Finished installing {ModList.Name}";
                        InstallState = InstallState.Success;
                    });
                }
                else
                {
                    RxApp.MainThreadScheduler.Schedule(() =>
                    {
                        InstallResult = result;
                        InstallState = InstallState.Failure;
                        ProgressText = $"Error during installation of {ModList.Name}";
                        ProgressPercent = Percent.Zero;
                        ProgressState = ProgressState.Error;
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                RxApp.MainThreadScheduler.Schedule(() =>
                {
                    InstallState = InstallState.Failure;
                    ProgressText = $"Error during installation of {ModList.Name}";
                    ProgressPercent = Percent.Zero;
                    ProgressState = ProgressState.Error;
                    InstallResult = Wabbajack.Installer.InstallResult.Errored;
                });
            }
        });

    }

    private async Task PrepareDownloaders()
    {
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
    }

    private void ShowMissingManualReport(Archive[] toArray)
    {
        _logger.LogInformation("Writing Manual helper report");
        var report = Installer.DownloadLocation.TargetPath.Combine("MissingManuals.html");
        var downloadsPath = Installer.DownloadLocation.TargetPath;
        {
            using var writer = new StreamWriter(report.Open(FileMode.Create, FileAccess.Write, FileShare.None));
            writer.Write("<html><head><title>Missing Files</title></head><body>");
            writer.Write("<h1>Missing Files</h1>");
            writer.Write(
                "<p>Wabbajack was unable to source the following files automatically. Please take manual action on the following files. You might find more information on these files in the modlist readme and/or Discord server.</p>");
            foreach (var archive in toArray)
            {
                switch (archive.State)
                {
                    case Manual manual:
                        writer.Write($"<h3>{archive.Name} (Manual)</h3>");
                        writer.Write($"<p>{manual.Prompt}</p>");
                        writer.Write($"<p>Put this file inside <a href='{Installer.DownloadLocation.TargetPath}'>your Wabbajack downloads folder.</a></p>");
                        writer.Write($"<p>Download URL: <a href=\"{manual.Url}\">{manual.Url}</a></p>");
                        break;
                    case MediaFire mediaFire:
                        writer.Write($"<h3>{archive.Name} (MediaFire)</h3>");
                        writer.Write($"<p>Put this file inside <a href='{Installer.DownloadLocation.TargetPath}'>your Wabbajack downloads folder.</a></p>");
                        writer.Write($"<p>Download URL: <a href=\"{mediaFire.Url}\">{mediaFire.Url}</a></p>");
                        break;
                    case GameFileSource gameFile:
                        writer.Write($"<h3>{archive.Name} (Game File)</h3>");
                        if(archive.Name.Contains("CreationKit"))
                        {
                            writer.Write($"<p>This modlist requires the Creation Kit to function.</p>");
                            if (ModList.GameType == Game.SkyrimSpecialEdition || ModList.GameType == Game.SkyrimVR)
                            {
                                writer.Write(@$"<p><a href=""steam://run/1946180"">Click here to install it via Steam.</a></p>");
                            }
                            else if(ModList.GameType == Game.Fallout4 || ModList.GameType == Game.Fallout4VR)
                            {
                                writer.Write(@$"<p><a href=""steam://run/1946160"">Click here to install it via Steam.</a></p>");
                            }
                            else if(ModList.GameType == Game.Starfield)
                            {
                                writer.Write(@$"<p><a href=""steam://run/2722710"">Click here to install it via Steam.</a></p>");
                            }
                        }
                        else if(ModList.GameType == Game.SkyrimSpecialEdition && archive.Name.Contains("curios", StringComparison.OrdinalIgnoreCase))
                        {
                            writer.Write("<p>This is a game file that commonly causes issues.</p>");
                            writer.Write(@"<p><a target='_blank' href='https://wiki.wabbajack.org/user_documentation/Troubleshooting%20FAQ.html#unable-to-download-curios-files'>Click here for more information on how to resolve the issue.</a></p>");
                        }
                        else if(ModList.GameType == Game.SkyrimSpecialEdition && archive.Name.StartsWith("Data_cc", StringComparison.OrdinalIgnoreCase))
                        {
                            writer.Write("<p>This is a Creation Club file that could not be found. Check if the Anniversary Edition DLC is installed before installing this modlist, and validate you have the same version as the one the modlist author has.</p>");
                        }
                        else
                        {
                            writer.Write("<p>This is a game file that could not be found. Validate the game is installed properly in the same language as that of the modlist author.</p>");
                        }
                        break;

                    case Mega mega:
                        writer.Write($"<h3>{archive.Name} (MEGA)</h3>");
                        writer.Write($"<p>Please <a href='{mega.Url.ToString()}' target='_blank'>click here to download this file</a>, then manually place it inside <a href='{Installer.DownloadLocation.TargetPath}'>the Wabbajack downloads directory.</a></p>");
                        break;

                    case IPS4OAuth2 ips4:
                        writer.Write($"<h3>{ips4.Name} (IPS4OAuth2)</h3>");
                        writer.Write($"<p>Please <a href='{ips4.LinkUrl.ToString()}' target='_blank'>click here to download this file</a>, then manually place it inside <a href='{Installer.DownloadLocation.TargetPath}'>the Wabbajack downloads directory.</a></p>");
                        break;

                    case GoogleDrive gd:
                        writer.Write($"<h3>Google Drive: {gd.Id}</h3>");
                        writer.Write($"<p>Please <a href='{gd.GetUri().ToString()}' target='_blank'>click here to download this file</a>, then manually place it inside <a href='{Installer.DownloadLocation.TargetPath}'>the Wabbajack downloads directory.</a></p>");
                        break;

                    case Http http:
                        writer.Write($"<h3>Direct download: {http.PrimaryKeyString}</h3>");
                        writer.Write($"<p>Please <a href='{http.Url.ToString()}' target='_blank'>click here to download this file</a>, then manually place it inside <a href='{Installer.DownloadLocation.TargetPath}'>the Wabbajack downloads directory.</a></p>");
                        break;

                    case Nexus nexus:
                        writer.Write($"<h3>{nexus.Description} (NexusMods)");
                        writer.Write($"<p>Please <a href='{nexus.LinkUrl}' target='_blank'>click here to download this file</a>, then manually place it inside <a href='{Installer.DownloadLocation.TargetPath}'>the Wabbajack downloads directory.</a></p>");
                        break;

                    default:
                        writer.Write($"<h3>{archive.Name}</h1>");
                        writer.Write($"<p>Unknown download type</p>");
                        writer.Write($"<p>Primary Key (may not be helpful): <a href=\"{archive.State.PrimaryKeyString}\">{archive.State.PrimaryKeyString}</a></p>");
                        break;
                }
            }

            writer.Write("</body></html>");
        }
        
        Process.Start(new ProcessStartInfo("cmd.exe", $"start /c \"{report}\"")
        {
            CreateNoWindow = true,
        });
    }

    class SavedInstallSettings
    {
        public AbsolutePath ModListLocation { get; set; }
        public AbsolutePath InstallLocation { get; set; }
        public AbsolutePath DownloadLocation { get; set; }
        
        public ModlistMetadata Metadata { get; set; }
    }

    private void PopulateSlideShow(ModList modList)
    {
        return;

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
        //SlideShowImage = ModListImage;
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
