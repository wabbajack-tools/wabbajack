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
using Wabbajack.Util;
using Wabbajack.CLI.Verbs;
using Microsoft.Extensions.DependencyInjection;
using Wabbajack.VFS;
using Humanizer;
using System.Text.RegularExpressions;
using System.Windows.Input;
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

    /// <summary>
    ///     What the failure screen says inline: the opening of a matched article, or the whole of one of the
    ///     three short messages that are not articles at all. It starts as the reason the panel is empty,
    ///     since the Error summary tab can be opened before anything has been diagnosed.
    /// </summary>
    [Reactive] public partial string FailureDetailsDescription { get; set; } =
        "Nothing has been diagnosed yet. If the install stops, choose \"How do I fix this?\" to check your log against known issues.";

    /// <summary>
    ///     The matched article's markdown, empty when the diagnosis produced no article.
    ///     <para>
    ///         These come from a community repository and carry headings, links to downloads and, for a few
    ///         of them, a screenshot, which is why they are rendered rather than shown as text. The renderer
    ///         is <see cref="DiagnosticsArticle" /> and the reader is the user's browser -
    ///         <see cref="OpenFailureArticleCommand" /> writes the page to a temp file and opens it.
    ///     </para>
    /// </summary>
    [Reactive] public partial string FailureArticleMarkdown { get; set; } = string.Empty;

    /// <summary>The screenshot a matched article may carry beside its text; null for most of them.</summary>
    [Reactive] public partial string? FailureArticleImage { get; set; }

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
    
    // Command properties
    public ICommand OpenManifestCommand { get; }
    public ICommand OpenReadmeCommand { get; }
    public ICommand OpenWikiCommand { get; }
    public ICommand OpenCommunityCommand { get; }
    public ICommand OpenWebsiteCommand { get; }
    public ICommand BackToGalleryCommand { get; }
    public ICommand DiagnoseFailureCommand { get; }
    public ICommand OpenFailureArticleCommand { get; }
    public ICommand OpenLogFolderCommand { get; }
    public ICommand OpenInstallFolderCommand { get; }
    public ICommand InstallCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand EditInstallDetailsCommand { get; }
    public ICommand VerifyCommand { get; }
    public ICommand CreateShortcutCommand { get; }
    
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

        OpenFailureArticleCommand = ReactiveCommand.Create(() => OpenFailureArticle(),
            this.WhenAnyValue(vm => vm.FailureArticleMarkdown, markdown => !string.IsNullOrWhiteSpace(markdown)));

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
                FailureArticleMarkdown = result.Body;
                FailureArticleImage = result.ImagePathOrUrl;

                var lead = DiagnosticsArticle.Lead(result.Body);
                FailureDetailsDescription = string.IsNullOrEmpty(lead)
                    ? "A community troubleshooting article matches your log."
                    : lead;
            }
            else
            {
                ClearFailureArticle();
                FailureDetailsTitle = "No common issues detected";
                FailureDetailsDescription = "Couldn't match a known issue in your log. You can open the log file to investigate further, or join the Wabbajack Discord to ask for help.";
            }
            InstallState = InstallState.Failure;
            DiagnosticsVisible = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Diagnostics failed");
            ClearFailureArticle();
            FailureDetailsTitle = "Diagnostics failed";
            FailureDetailsDescription = $"An error occurred while analyzing your log.\n{ex.GetType().Name}: {ex.Message}";
            InstallState = InstallState.Failure;
            DiagnosticsVisible = true;
        }
    }

    private void ClearFailureArticle()
    {
        FailureArticleMarkdown = string.Empty;
        FailureArticleImage = null;
    }

    /// <summary>
    ///     Renders the matched article and hands it to the user's browser.
    ///     <para>
    ///         The page used to be drawn by an embedded WebView2 through <c>NavigateToString</c>. The same
    ///         HTML now goes to a file from <see cref="TemporaryFileManager" /> - so it is cleaned up with
    ///         everything else the run left behind - and is opened with the shell, which keeps the headings,
    ///         the links to downloads and the screenshots that plain text would have lost.
    ///     </para>
    /// </summary>
    private void OpenFailureArticle()
    {
        try
        {
            var html = DiagnosticsArticle.BuildHtml(FailureDetailsTitle, FailureArticleMarkdown,
                FailureArticleImage, CurrentArticleTheme());

            var manager = _serviceProvider.GetRequiredService<TemporaryFileManager>();
            var file = manager.CreateFolder().Path.Combine(DiagnosticsArticle.FileName(FailureDetailsTitle));

            file.WriteAllText(html);
            UIUtils.OpenFile(file);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not open the diagnostics article");
        }
    }

    /// <summary>
    ///     The app's own colours, so the article does not arrive as black-on-white in the middle of a dark
    ///     theme. Falls back to the same literals the embedded renderer used when a brush is missing.
    /// </summary>
    private static ArticleTheme CurrentArticleTheme()
    {
        static string ToCss(System.Windows.Media.Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

        var resources = System.Windows.Application.Current?.Resources;

        System.Windows.Media.Color? Brush(string key) =>
            resources?[key] is System.Windows.Media.SolidColorBrush brush ? brush.Color : null;

        var fallback = ArticleTheme.Default;

        return new ArticleTheme(
            Brush("ForegroundBrush") is { } fg ? ToCss(fg) : fallback.Foreground,
            Brush("CardBackgroundBrush") is { } bg ? ToCss(bg) : fallback.Background,
            Brush("PrimaryBrush") is { } accent ? ToCss(accent) : fallback.Accent);
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
                Metadata = ModlistMetadata
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
