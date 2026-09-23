using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using ReactiveUI;
using System.Reactive.Disposables;
using System.Windows.Media.Imaging;
using ReactiveUI.SourceGenerators;
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
using Wabbajack.Installer.Preflight;
using Wabbajack.LoginManagers;
using Wabbajack.Messages;
using Wabbajack.Models;
using Wabbajack.Paths;
using Wabbajack.RateLimiter;
using Wabbajack.Paths.IO;
using Wabbajack.Services.OSIntegrated;
using Wabbajack.Networking.Steam;
using Wabbajack.Translation;
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
using Wabbajack.Reporting;
using Markdig;
using Markdig.Syntax;


namespace Wabbajack;

public enum InstallState
{
    Configuration,
    Preflight,
    Installing,
    Success,
    Failure
}

public partial class InstallationVM : ProgressViewModel, ICpuStatusVM
{
    private const string LastLoadedModlist = "last-loaded-modlist";
    private const string InstallSettingsPrefix = "install-settings-";
    private readonly Random _random = new();
    

    [Reactive] public partial ModList ModList { get; set; }
    [Reactive] public partial ModlistMetadata ModlistMetadata { get; set; }
    [Reactive] public partial FilePickerVM WabbajackFileLocation { get; set; }
    [Reactive] public partial MO2InstallerVM Installer { get; set; }
    [Reactive] public partial StandardInstaller StandardInstaller { get; set; }
    [Reactive] public partial BitmapImage ModListImage { get; set; }
    [Reactive] public partial InstallState InstallState { get; set; }

    /// <summary>The page between the folder picker and the install; null outside <see cref="InstallState.Preflight" />.</summary>
    [Reactive] public partial PreflightVM? Preflight { get; set; }

    [Reactive] public partial string FailureDetailsTitle { get; set; } = string.Empty;
    [Reactive] public partial string FailureDetailsDescription { get; set; } = string.Empty;

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
    [Reactive] public partial BitmapFrame SlideShowImage { get; set; }
    [Reactive] public partial string SlideShowTitle { get; set; } 
    [Reactive] public partial string SlideShowAuthor { get; set; }
    [Reactive] public partial string SlideShowDescription { get; set; }
    [Reactive] public partial string SuggestedInstallFolder { get; set; }
    [Reactive] public partial string SuggestedDownloadFolder { get; set; }

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
    private readonly NexusLoginManager _nexusLoginManager;
    private CancellationTokenSource _cancellationTokenSource;

    /// <summary>How long a shutdown gives the preflight download watcher to stop before it gives up on it.</summary>
    private static readonly TimeSpan PreflightShutdownTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    ///     The shutdown started by the handoff into the installer, kept so a window closing in that moment
    ///     still has something to wait on after <see cref="Preflight" /> has been cleared.
    /// </summary>
    private Task? _preflightShutdown;

    public ReadOnlyObservableCollection<CPUDisplayVM> StatusList => _resourceMonitor.Tasks;

    [Reactive] public partial bool Installing { get; set; }
    
    [Reactive] public partial ValidationResult ValidationResult { get; set; }
    
    [Reactive] public partial bool ShowNSFWSlides { get; set; }

    [Reactive] public partial bool DiagnosticsVisible { get; set; } = false;

    public LogStream LoggerProvider { get; }


    [Reactive] public partial string HashingSpeed { get; set; }
    [Reactive] public partial string ExtractingSpeed { get; set; }
    [Reactive] public partial string DownloadingSpeed { get; set; }

    [Reactive] public partial IReadOnlyList<GameLanguage> TranslationLanguageOptions { get; set; } = [];
    [Reactive] public partial bool TranslationSupported { get; set; }
    [Reactive] public partial bool TranslateModlist { get; set; }
    [Reactive] public partial GameLanguage? TranslationLanguage { get; set; }
    [Reactive] public partial bool DownloadTranslationVoices { get; set; }
    [Reactive] public partial string TranslationVoicesStatus { get; set; } = string.Empty;
    private bool _restoringTranslationSettings;
    private bool _choosingTranslationVoices;
    private string _translationVoicesNote = string.Empty;
    
    // Command properties
    public ICommand OpenManifestCommand { get; }
    public ICommand OpenReadmeCommand { get; }
    public ICommand OpenWikiCommand { get; }
    public ICommand OpenCommunityCommand { get; }
    public ICommand OpenWebsiteCommand { get; }
    public ICommand BackToGalleryCommand { get; }
    public ICommand DiagnoseFailureCommand { get; }
    public ICommand OpenLogFolderCommand { get; }
    public ICommand OpenInstallFolderCommand { get; }
    public ICommand InstallCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand EditInstallDetailsCommand { get; }
    public ICommand VerifyCommand { get; }
    public ICommand CreateShortcutCommand { get; }
    public ICommand ChooseTranslationVoicesCommand { get; }
    
    public InstallationVM(ILogger<InstallationVM> logger, DTOSerializer dtos, SettingsManager settingsManager, IServiceProvider serviceProvider,
        SystemParametersConstructor parametersConstructor, IGameLocator gameLocator, LogStream loggerProvider, ResourceMonitor resourceMonitor,
        Services.OSIntegrated.Configuration configuration, HttpClient client, NexusLoginManager nexusLoginManager)
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
        _nexusLoginManager = nexusLoginManager;

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
        ChooseTranslationVoicesCommand = ReactiveCommand.Create(() => ChooseTranslationVoices().FireAndForget(),
            this.WhenAnyValue(vm => vm.TranslateModlist, vm => vm.TranslationLanguage,
                (enabled, language) => enabled && language is {HasVoices: true}));
        this.WhenAnyValue(vm => vm.TranslateModlist, vm => vm.TranslationLanguage)
            .Select(t => (Enabled: t.Item1, Language: t.Item2?.Id))
            .Where(t => !t.Enabled || t.Language != null)
            .DistinctUntilChanged()
            .Where(_ => !_restoringTranslationSettings)
            .Skip(1)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => ChooseTranslationVoices().FireAndForget());

        InstallCommand = ReactiveCommand.Create(() => BeginPreflight().FireAndForget(), this.WhenAnyValue(vm => vm.LoadingLock.IsNotLoading,
                                                                                                        vm => vm.ValidationResult,
                                                                                                       (notLoading, validationResult) => notLoading && (validationResult?.Succeeded ?? false)));

        OpenReadmeCommand = ReactiveCommand.Create(() =>
        {
            UIUtils.OpenWebsite(ModList.Readme);
        }, this.WhenAnyValue(vm => vm.LoadingLock.IsNotLoading, vm => vm.ModList.Readme, (isNotLoading, readme) => isNotLoading && !string.IsNullOrWhiteSpace(readme)));

        OpenWebsiteCommand = ReactiveCommand.Create(() =>
        {
            UIUtils.OpenWebsite(ModList.Website);
        }, this.WhenAnyValue(vm => vm.LoadingLock.IsNotLoading, vm => vm.ModList.Website, (isNotLoading, website) => isNotLoading && !string.IsNullOrWhiteSpace(website)));
        
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

        OpenCommunityCommand = ReactiveCommand.Create(() =>
        {
            UIUtils.OpenWebsite(new Uri(ModList.Community));
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

        BackToGalleryCommand = ReactiveCommand.Create(() => NavigateToGlobal.Send(ScreenType.ModListGallery));

        DiagnoseFailureCommand = ReactiveCommand.Create(() => LaunchDiagnostics());

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

            this.WhenAnyValue(vm => vm.WabbajackFileLocation.ValidationResult,
                  vm => vm.Installer.DownloadLocation.ValidationResult,
                  vm => vm.Installer.Location.ValidationResult,
                  vm => vm.WabbajackFileLocation.TargetPath,
                  vm => vm.Installer.Location.TargetPath,
                  vm => vm.Installer.DownloadLocation.TargetPath)
                .CombineLatest(this.WhenAnyValue(vm => vm.TranslateModlist, vm => vm.TranslationLanguage), (t, _) => t)
                .Select(t =>
                {
                    var (wjVr, dlVr, instVr, wjPath, instPath, dlPath) = t;

                    var errors = new[] { wjVr, dlVr, instVr }
                        .Where(v => v != null && v.Failed)
                        .Concat(Validate())
                        .ToArray();

                    if (!errors.Any()) return ValidationResult.Success;

                    var reasons = errors.Select(e => e.Reason)
                        .Where(r => !string.IsNullOrWhiteSpace(r))
                        .ToArray();

                    foreach (var e in errors)
                    {
                        if (e.Failed && string.IsNullOrWhiteSpace(e.Reason))
                            _logger.LogWarning("ValidationResult failed but had no reason. Type={Type}", e.GetType().FullName);
                    }

                    return reasons.Length == 0
                        ? ValidationResult.Fail("Validation failed (no reason provided).")
                        : ValidationResult.Fail(string.Join("\n", reasons));
                })
                .BindTo(this, vm => vm.ValidationResult)
                .DisposeWith(disposables);

            this.WhenAny(vm => vm.InstallState)
                .ObserveOnGuiThread()
                .Subscribe(state =>
                    {
                        CurrentStep = state switch
                        {
                            InstallState.Configuration => Step.Configuration,
                            InstallState.Preflight => Step.Busy,
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

    private void LaunchDiagnostics()
    {
        try
        {
            // Remote tags
            const string YamlUrl = "https://raw.githubusercontent.com/JanuarySnow/WJ-Bot/main/tags.yaml";
            const string RawBase = "https://raw.githubusercontent.com/JanuarySnow/WJ-Bot/main/";

            // try and get the log without Wabbajack stepping on us doing that ting
            var liveLog = _configuration.LogLocation.Combine("Wabbajack.current.log").ToString();

            string logText = SafeReadAllText(liveLog)
                          ?? string.Empty;

            var result = Wabbajack.Reporting.DiagnosticsFacade.AnalyzeText(
                logText: logText,
                yamlUrl: YamlUrl,
                rawBase: RawBase
            );

            if (result.HasValue)
            {
                FailureDetailsTitle = $"Possible issue: {result.Title}";
                FailureDetailsDescription = result.Body;
            }
            else
            {
                FailureDetailsTitle = "No common issues detected";
                FailureDetailsDescription = "Couldn't match a known issue in your log. You can open the log file to investigate further, or join the Wabbajack Discord to ask for help.";
            }
            InstallState = InstallState.Failure;
            DiagnosticsVisible = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Diagnostics failed");
            FailureDetailsTitle = "Diagnostics failed";
            FailureDetailsDescription = $"An error occurred while analyzing your log.\n{ex.GetType().Name}: {ex.Message}";
            InstallState = InstallState.Failure;
            DiagnosticsVisible = true;
        }
    }

    private static string? SafeReadAllText(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            using var fs = new FileStream(path,
                                          FileMode.Open,
                                          FileAccess.Read,
                                          FileShare.ReadWrite | FileShare.Delete);
            using var sr = new StreamReader(fs);
            return sr.ReadToEnd();
        }
        catch
        {
            return null;
        }
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

            case InstallState.Preflight:
                CancelPreflight();
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

        if (TranslateModlist && TranslationSupported && TranslationLanguage == null)
            yield return ValidationResult.Fail("Select a language to translate to");

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
        // A load from the gallery or the protocol handler can arrive while a preflight is running.
        DisposePreflight();
        InstallState = InstallState.Configuration;
        WabbajackFileLocation.TargetPath = path;
        try
        {
            ModList = await StandardInstaller.LoadFromFile(_dtos, path);
            var translationSupport = TranslationGames.For(ModList.GameType);
            TranslationSupported = translationSupport != null;
            TranslationLanguageOptions = translationSupport?.Languages ?? [];
            if (TranslationLanguage != null && !TranslationLanguageOptions.Contains(TranslationLanguage))
                TranslationLanguage = null;
            if (!TranslationSupported) TranslateModlist = false;
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
                _logger.LogDebug("Modlist metadata not loaded, possibly using local install without metadata file");
            }

            if (hasPrevModListInstallation)
            {
                Installer.Location.TargetPath = prevSettings.InstallLocation;
                Installer.DownloadLocation.TargetPath = prevSettings.DownloadLocation;
                _restoringTranslationSettings = true;
                TranslateModlist = TranslationSupported && prevSettings.TranslateModlist;
                TranslationLanguage = TranslationGames.For(ModList.GameType)?.Find(prevSettings.TranslationLanguage ?? "");
                DownloadTranslationVoices = prevSettings.DownloadTranslationVoices && TranslationLanguage is {HasVoices: true};
                _restoringTranslationSettings = false;
                UpdateTranslationVoicesStatus();
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

    /// <summary>
    ///     Install from the folder page opens the preflight page. The runner gets everything the installer
    ///     will, and the installer only starts once every check is satisfied.
    /// </summary>
    private async Task BeginPreflight()
    {
        DisposePreflight();

        ConfigurationText = "Preparation";
        ProgressText = "Preflight";
        ProgressPercent = Percent.Zero;
        CurrentStep = Step.Busy;
        InstallState = InstallState.Preflight;
        ProgressState = ProgressState.Normal;

        try
        {
            var postfix = (await WabbajackFileLocation.TargetPath.FileName.ToString().Hash()).ToHex();
            await _settingsManager.Save(InstallSettingsPrefix + postfix, new SavedInstallSettings
            {
                ModListLocation = WabbajackFileLocation.TargetPath,
                InstallLocation = Installer.Location.TargetPath,
                DownloadLocation = Installer.DownloadLocation.TargetPath,
                Metadata = ModlistMetadata,
                TranslateModlist = TranslateModlist,
                TranslationLanguage = TranslationLanguage?.Id,
                DownloadTranslationVoices = DownloadTranslationVoices
            });
            await _settingsManager.Save(LastLoadedModlist, WabbajackFileLocation.TargetPath);

            // Always reload the modlist, incase of retrying , so it gets fresh directives and archives
            var freshModList = await StandardInstaller.LoadFromFile(
                _dtos, WabbajackFileLocation.TargetPath);

            var canSource = GameRegistry.Games[freshModList.GameType].CanSourceFrom ?? Array.Empty<Game>();
            var namedgames = freshModList.OtherGames;
            var validgames = new List<Game>();
            foreach (var g in namedgames)
            {
                if (canSource.Contains(g) && GameRegistry.Games.ContainsKey(g))
                    validgames.Add(g);
            }

            var cfg = new InstallerConfiguration
            {
                Game = ModList.GameType,
                OtherGames = validgames.ToArray(),
                Downloads = Installer.DownloadLocation.TargetPath,
                Install = Installer.Location.TargetPath,
                ModList = freshModList,
                ModlistArchive = WabbajackFileLocation.TargetPath,
                SystemParameters = _parametersConstructor.Create(),
                // A game that cannot be found is the game-installed check's finding, not an exception here.
                GameFolder = _gameLocator.TryFindLocation(freshModList.GameType, out var gameFolder) ? gameFolder : default,
                Metadata = ModlistMetadata
            };

            var runner = PreflightRunner.Create(_serviceProvider, cfg);
            var preflight = new PreflightVM(runner, _nexusLoginManager, this, _logger, _serviceProvider,
                this.WhenAnyValue(x => x.DownloadingSpeed), OpenReadmeCommand, OpenWebsiteCommand,
                OpenCommunityCommand, OpenManifestCommand);
            preflight.InstallCommand
                .Subscribe(_ => RunInstaller(cfg).FireAndForget())
                .DisposeWith(preflight.CompositeDisposable);
            preflight.BackCommand
                .Subscribe(_ => CancelPreflight())
                .DisposeWith(preflight.CompositeDisposable);

            Preflight = preflight;
            preflight.Start();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "While preparing to install {Modlist}", ModList.Name);
            InstallState = InstallState.Failure;
            ProgressText = $"Error during installation of {ModList.Name}";
            ProgressPercent = Percent.Zero;
            ProgressState = ProgressState.Error;
            InstallResult = Wabbajack.Installer.InstallResult.Errored;
            LaunchDiagnostics();
        }
    }

    /// <summary>Back from the preflight page. The pickers were never touched, so it is the edit-details path.</summary>
    private void CancelPreflight()
    {
        DisposePreflight();
        EditInstallDetailsCommand.Execute(null);
    }

    private void DisposePreflight()
    {
        var preflight = Preflight;
        if (preflight == null) return;
        Preflight = null;
        preflight.Dispose();
    }

    /// <summary>
    ///     Stops a preflight in progress so the application can shut down. The state stays at Preflight until
    ///     the watcher has stopped or the wait runs out, so a caller watching for the install to end gives a
    ///     copy in progress time to unwind instead of leaving a partial file behind. The state is cleared from
    ///     whatever thread finishes the wait: the caller is blocking this one, so nothing posted to it would
    ///     ever run.
    /// </summary>
    public void CancelPreflightForShutdown()
    {
        if (InstallState != InstallState.Preflight) return;

        var preflight = Preflight;
        Preflight = null;

        // Preflight is cleared before the handoff into the installer awaits its shutdown, so a window
        // closing in that moment finds nothing here and has to wait on that shutdown instead.
        var stopping = preflight?.StopWatcherAsync() ?? _preflightShutdown;
        if (stopping == null || stopping.IsCompleted)
        {
            InstallState = InstallState.Configuration;
            return;
        }

        stopping
            .WaitAsync(PreflightShutdownTimeout)
            .ContinueWith(_ => InstallState = InstallState.Configuration, TaskScheduler.Default);
    }

    private async Task RunInstaller(InstallerConfiguration cfg)
    {
        // The watcher must be done moving files before the installer hashes the downloads folder.
        var preflight = Preflight;
        if (preflight != null)
        {
            Preflight = null;
            _preflightShutdown = preflight.ShutdownAsync();
            await _preflightShutdown;
        }

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

            try
            {
                StandardInstaller = StandardInstaller.Create(_serviceProvider, cfg);


                StandardInstaller.OnStatusUpdate = update =>
                {
                    RxApp.MainThreadScheduler.Schedule(() =>
                    {
                        ProgressText = update.StatusText;
                        ProgressPercent = update.StepsProgress;
                    });
                };

                StandardInstaller.OnConfirmAction = async (title, message) =>
                {
                    var tcs = new TaskCompletionSource<bool>();
                    RxApp.MainThreadScheduler.Schedule(async () =>
                    {
                        var mainWindowVM = (MainWindowVM)System.Windows.Application.Current.MainWindow.DataContext;
                        var result = await mainWindowVM.ShowConfirmationDialog(title, message);
                        tcs.TrySetResult(result);
                    });
                    return await tcs.Task;
                };

                _logger.LogInformation("Starting installation of {modlist} {version}:", cfg.ModList.Name, cfg.ModList.Version?.ToString() ?? "");
                _logger.LogInformation("    Installation folder: {installFolder}", cfg.Install.ToString());
                _logger.LogInformation("    Downloads folder: {downloadsFolder}", cfg.Downloads.ToString());
                _logger.LogInformation("    Modlist file location: {wjFileLocation}", cfg.ModlistArchive.ToString());
                _logger.LogInformation("    Game: {game}", cfg.Game.ToString());
                _logger.LogInformation("    Game folder: {gameFolder}", cfg.GameFolder);
                _logger.LogInformation("    Other games that can be sourced from: {otherGames}", string.Join(", ", cfg.OtherGames.Select(g => g.ToString())));

                InstallResult result;
                var translationOutcome = string.Empty;
                using (_cancellationTokenSource = new CancellationTokenSource())
                {
                    result = await StandardInstaller.Begin(_cancellationTokenSource.Token);
                    if (result == Wabbajack.Installer.InstallResult.Succeeded && TranslateModlist &&
                        TranslationSupported && TranslationLanguage != null)
                        translationOutcome = await RunTranslation(cfg, TranslationLanguage,
                            _cancellationTokenSource.Token);
                }
                if (result == Wabbajack.Installer.InstallResult.Succeeded)
                {
                    RxApp.MainThreadScheduler.Schedule(() =>
                    {
                        InstallResult = result;
                        ProgressText = $"Finished installing {ModList.Name}{translationOutcome}";
                        ProgressPercent = Percent.One;
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
                        LaunchDiagnostics();
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
                    LaunchDiagnostics();
                });
            }
        });

    }

    partial class SavedInstallSettings
    {
        public AbsolutePath ModListLocation { get; set; }
        public AbsolutePath InstallLocation { get; set; }
        public AbsolutePath DownloadLocation { get; set; }
        
        public ModlistMetadata Metadata { get; set; }

        public bool TranslateModlist { get; set; }
        public string? TranslationLanguage { get; set; }
        public bool DownloadTranslationVoices { get; set; }
    }

    private void UpdateTranslationVoicesStatus()
    {
        var language = TranslationLanguage;
        TranslationVoicesStatus = !TranslateModlist || language == null
            ? string.Empty
            : !language.HasVoices
                ? $"{language.DisplayName} has no official voice files, so voices stay in English."
                : DownloadTranslationVoices
                    ? $"{language.DisplayName} voice files will be downloaded from {(language.Voices is NexusVoices ? "Nexus Mods" : "Steam")} after the install.{_translationVoicesNote}"
                    : $"Voices stay in English.{_translationVoicesNote}";
    }

    private async Task ChooseTranslationVoices()
    {
        // Stops a second prompt and a second Steam login while one is open
        if (_choosingTranslationVoices) return;
        _choosingTranslationVoices = true;
        try
        {
            await ChooseTranslationVoicesOnce();
        }
        finally
        {
            _choosingTranslationVoices = false;
        }
    }

    private async Task ChooseTranslationVoicesOnce()
    {
        var language = TranslationLanguage;
        _translationVoicesNote = string.Empty;
        if (!TranslateModlist || language is not {HasVoices: true})
        {
            DownloadTranslationVoices = false;
            UpdateTranslationVoicesStatus();
            return;
        }

        var mainWindowVM = (MainWindowVM)System.Windows.Application.Current.MainWindow.DataContext;
        var steam = language.Voices is SteamVoices;
        var message = steam
            ? $"Wabbajack can download {language.Voices!.Description}. This needs a one time Steam login inside Wabbajack.\n\n" +
              "Your Steam password is only ever sent to Steam. Wabbajack does not store it and does not share it " +
              "with anyone. Wabbajack keeps an encrypted Steam login token on this PC so it can download the game " +
              "files you own, the same way the Steam client does, and you can log out at any time from Settings, Logins.\n\n"
            : $"Wabbajack can download {language.Voices!.Description}. It is installed as loose voice files over the " +
              "English ones and is downloaded with your Nexus Mods login, like the translation files.\n\n";
        var wanted = await mainWindowVM.ShowConfirmationDialog($"Download {language.DisplayName} voice files?",
            message + $"If you choose Cancel, the game text is still translated to {language.DisplayName} and the voices stay in English.");
        if (!wanted)
        {
            DownloadTranslationVoices = false;
            UpdateTranslationVoicesStatus();
            return;
        }

        if (!steam)
        {
            DownloadTranslationVoices = true;
            UpdateTranslationVoicesStatus();
            return;
        }

        var (loggedIn, note) = await EnsureSteamLogin();
        _translationVoicesNote = note;
        DownloadTranslationVoices = loggedIn;
        UpdateTranslationVoicesStatus();
    }

    private async Task<(bool LoggedIn, string Note)> EnsureSteamLogin()
    {
        var session = _serviceProvider.GetRequiredService<ISteamSession>();
        if (session.IsLoggedIn) return (true, " Already logged into Steam.");
        if (session.HaveStoredToken)
        {
            try
            {
                var result = await session.LoginWithStoredTokenAsync(CancellationToken.None);
                return (true, $" Using the saved Steam login for {result.AccountName}.");
            }
            catch (Exception ex)
            {
                _logger.LogInformation("Stored Steam login was not accepted: {Message}", ex.Message);
            }
        }

        var pane = _serviceProvider.GetRequiredService<SteamLoginVM>();
        try
        {
            return await ShowSteamLogin.Send(pane)
                ? (true, string.Empty)
                : (false, " The Steam login did not complete; use Voice files to try again.");
        }
        finally
        {
            pane.Dispose();
        }
    }

    private async Task<string> RunTranslation(InstallerConfiguration cfg, GameLanguage language,
        CancellationToken token)
    {
        try
        {
            var runner = _serviceProvider.GetRequiredService<TranslationRunner>();
            var progress = new OrderedProgress<TranslationProgress>(p => RxApp.MainThreadScheduler.Schedule(() =>
            {
                ProgressText = $"{p.Stage}: {p.Text}";
                ProgressPercent = Percent.FactoryPutInRange(p.Fraction);
            }));
            var manual = _serviceProvider.GetRequiredService<WpfManualTranslationDownloads>();
            manual.LanguageName = language.DisplayName;
            var summary = await runner.Run(
                new TranslationRequest(cfg.Install, cfg.Downloads, cfg.Game, cfg.GameFolder, language,
                    DownloadTranslationVoices),
                manual, progress, token);
            var missed = summary.TranslationFilesFound - summary.TranslationFilesDownloaded;
            var profiles = summary.Profiles.Count > 1 ? $" in {summary.Profiles.Count} profiles" : "";
            return $". {summary.PluginsTranslated} plugins translated to {language.DisplayName}{profiles}" +
                   (missed > 0 ? $", {missed} translation files could not be downloaded" : "") +
                   (summary.VoiceArchivesFailed.Count > 0 ? ", some voice files failed to download" : "") +
                   string.Concat(summary.Warnings.Select(w => ". " + w));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Translating the modlist to {Language} failed", language.DisplayName);
            return $". Translation to {language.DisplayName} failed, see the log";
        }
    }

    private sealed class OrderedProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
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
