using DynamicData.Binding;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orc.FileAssociation;
using Wabbajack.Common;
using Wabbajack.DTOs.Interventions;
using Wabbajack.Interventions;
using Wabbajack.Messages;
using Wabbajack.Models;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.UserIntervention;
using Wabbajack.ViewModels;
using System.Reactive.Concurrency;
using Wabbajack.Util;
using System.IO;
using System.Net.Http;

namespace Wabbajack;

/// <summary>
/// Main View Model for the application.
/// Keeps track of which sub view is being shown in the window, and has some singleton wiring like WorkQueue and Logging.
/// </summary>
public class MainWindowVM : ViewModel
{
    private Common.AsyncLock _browserLocker = new();
    public MainWindow MainWindow { get; }

    [Reactive]
    public ViewModel ActivePane { get; private set; }

    [Reactive]
    public ViewModel? ActiveFloatingPane { get; private set; } = null;

    [Reactive]
    public NavigationVM NavigationVM { get; private set; }

    public ObservableCollectionExtended<IStatusMessage> Log { get; } = new ObservableCollectionExtended<IStatusMessage>();

    public readonly CompilerHomeVM CompilerHomeVM;
    public readonly CompilerDetailsVM CompilerDetailsVM;
    public readonly CompilerFileManagerVM CompilerFileManagerVM;
    public readonly CompilerMainVM CompilerMainVM;
    public readonly InstallationVM InstallerVM;
    public readonly SettingsVM SettingsPaneVM;
    public readonly ModListGalleryVM GalleryVM;
    public readonly HomeVM HomeVM;
    public readonly WebBrowserVM WebBrowserVM;
    public readonly ModListDetailsVM ModListDetailsVM;
    public readonly InfoVM InfoVM;
    public readonly FileUploadVM FileUploadVM;
    public readonly MegaLoginVM MegaLoginVM;
    public readonly UserInterventionHandlers UserInterventionHandlers;

    private readonly Client _wjClient;
    private readonly ILogger<MainWindowVM> _logger;
    private readonly ResourceMonitor _resourceMonitor;
    private readonly SystemParametersConstructor _systemParams;
    private readonly IServiceProvider _serviceProvider;

    public ICommand CopyVersionCommand { get; }
    public ICommand ShowLoginManagerVM { get; }
    public ICommand GetHelpCommand { get; }
    public ICommand LoadLocalFileCommand { get; }
    public ICommand MinimizeCommand { get; }
    public ICommand MaximizeCommand { get; }
    public ICommand CloseCommand { get; }

    public string VersionDisplay { get; }

    [Reactive]
    public string ResourceStatus { get; set; }

    [Reactive]
    public string WindowTitle { get; set; }

    [Reactive]
    public bool UpdateAvailable { get; private set; }

    [Reactive]
    public bool NavigationVisible { get; private set; } = true;

    public MainWindowVM(ILogger<MainWindowVM> logger, Client wjClient,
        IServiceProvider serviceProvider, HomeVM homeVM, ModListGalleryVM modListGalleryVM, ResourceMonitor resourceMonitor,
        InstallationVM installerVM, CompilerHomeVM compilerHomeVM, CompilerDetailsVM compilerDetailsVM, CompilerFileManagerVM compilerFileManagerVM, CompilerMainVM compilerMainVM, SettingsVM settingsVM, WebBrowserVM webBrowserVM, NavigationVM navigationVM, InfoVM infoVM, ModListDetailsVM modlistDetailsVM, FileUploadVM fileUploadVM, MegaLoginVM megaLoginVM, SystemParametersConstructor systemParams, HttpClient httpClient)
    {
        _logger = logger;
        _wjClient = wjClient;
        _resourceMonitor = resourceMonitor;
        _serviceProvider = serviceProvider;
        _systemParams = systemParams;
        ConverterRegistration.Register();
        InstallerVM = installerVM;
        CompilerHomeVM = compilerHomeVM;
        CompilerDetailsVM = compilerDetailsVM;
        CompilerFileManagerVM = compilerFileManagerVM;
        CompilerMainVM = compilerMainVM;
        SettingsPaneVM = settingsVM;
        GalleryVM = modListGalleryVM;
        HomeVM = homeVM;
        WebBrowserVM = webBrowserVM;
        NavigationVM = navigationVM;
        InfoVM = infoVM;
        ModListDetailsVM = modlistDetailsVM;
        FileUploadVM = fileUploadVM;
        MegaLoginVM = megaLoginVM;
        UserInterventionHandlers = new UserInterventionHandlers(serviceProvider.GetRequiredService<ILogger<UserInterventionHandlers>>(), this);

        this.WhenAnyValue(x => x.ActiveFloatingPane)
            .Buffer(2, 1)
            .Select(b => (Previous: b[0], Current: b[1]))
            .Subscribe(x =>
            {
                x.Previous?.Activator.Deactivate();
                x.Current?.Activator.Activate();
            });

        MessageBus.Current.Listen<NavigateToGlobal>()
            .Subscribe(m => HandleNavigateTo(m.Screen))
            .DisposeWith(CompositeDisposable);

        MessageBus.Current.Listen<NavigateTo>()
            .Subscribe(m => HandleNavigateTo(m.ViewModel))
            .DisposeWith(CompositeDisposable);

        MessageBus.Current.Listen<ShowBrowserWindow>()
            .ObserveOnGuiThread()
            .Subscribe(HandleShowBrowserWindow)
            .DisposeWith(CompositeDisposable);

        MessageBus.Current.Listen<ShowNavigation>()
            .ObserveOnGuiThread()
            .Subscribe((_) => NavigationVisible = true)
            .DisposeWith(CompositeDisposable);

        MessageBus.Current.Listen<HideNavigation>()
            .ObserveOnGuiThread()
            .Subscribe((_) => NavigationVisible = false)
            .DisposeWith(CompositeDisposable);

        MessageBus.Current.Listen<ShowFloatingWindow>()
            .ObserveOnGuiThread()
            .Subscribe(m => HandleShowFloatingWindow(m.Screen))
            .DisposeWith(CompositeDisposable);

        _resourceMonitor.Updates
            .Select(r => string.Join(", ", r.Where(r => r.Throughput > 0)
            .Select(s => $"{s.Name} - {s.Throughput.ToFileSizeString()}/s")))
            .BindToStrict(this, view => view.ResourceStatus);


        if (IsStartingFromModlist(out var path))
        {
            LoadModlist(path);
        }
        else
        {
            // Start on mode selection
            NavigateToGlobal.Send(ScreenType.Home);
        }

        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var assemblyLocation = assembly.Location;
            var processLocation = Process.GetCurrentProcess().MainModule?.FileName ?? throw new Exception("Process location is unavailable!");

            var fvi = FileVersionInfo.GetVersionInfo(string.IsNullOrWhiteSpace(assemblyLocation) ? processLocation : assemblyLocation);
            Consts.CurrentMinimumWabbajackVersion = Version.Parse(fvi.FileVersion);

            _logger.LogInformation("Wabbajack information:");
            _logger.LogInformation("    Version: {FileVersion}", fvi.FileVersion);
            _logger.LogInformation("    Build: {Sha}", ThisAssembly.Git.Sha);
            _logger.LogInformation("    Entry point: {EntryPoint}", KnownFolders.EntryPoint);
            _logger.LogInformation("    Assembly Location: {AssemblyLocation}", assemblyLocation);
            _logger.LogInformation("    Process Location: {ProcessLocation}", processLocation);

            WindowTitle = Consts.AppName;

            _logger.LogInformation("General information:");
            _logger.LogInformation("    Windows version: {Version}", Environment.OSVersion.VersionString);

            var p = _systemParams.Create();

            _logger.LogInformation("System information: ");
            _logger.LogInformation("    GPU: {GpuName} ({VRAM})", p.GpuName, p.VideoMemorySize.ToFileSizeString());
            _logger.LogInformation("    RAM: {MemorySize}", p.SystemMemorySize.ToFileSizeString());
            _logger.LogInformation("    Primary display resolution: {ScreenWidth}x{ScreenHeight}", p.ScreenWidth, p.ScreenHeight);
            _logger.LogInformation("    Pagefile: {PageSize}", p.SystemPageSize.ToFileSizeString());
            _logger.LogInformation("    VideoMemorySizeMb (ENB): {EnbLEVRAMSize}", p.EnbLEVRAMSize.ToString());

            try
            {
                _logger.LogInformation("System partitions: ");
                var drives = DriveHelper.Drives;
                var partitions = DriveHelper.Partitions;
                foreach (var drive in drives)
                {
                    if (!drive.IsReady || drive.DriveType != DriveType.Fixed) continue;
                    var driveType = partitions[drive.RootDirectory.Name[0]].MediaType.ToString();
                    var rootDir = drive.RootDirectory.ToString();
                    var freeSpace = drive.AvailableFreeSpace.ToFileSizeString();
                    _logger.LogInformation("    {RootDir} ({DriveType}): {FreeSpace} free", rootDir, driveType, freeSpace);
                }
            }
            catch(Exception ex)
            {
                _logger.LogWarning("Failed to retrieve drive information: {ex}", ex.ToString());
            }

            try
            {
                Task.Run(async () =>
                {
                    var response = await httpClient.GetAsync(Consts.TlsInfoUri);
                    var content = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation("TLS Information: {content}", content);
                });
            }
            catch(Exception ex)
            {
                _logger.LogError("An error occurred while retrieving TLS information: {ex}", ex.ToString());
            }

            if (p.SystemPageSize == 0)
                _logger.LogWarning("Pagefile is disabled! This will cause issues such as crashing with Wabbajack and other applications!");


            Task.Run(() => _wjClient.SendMetric("started_wabbajack", fvi.FileVersion)).FireAndForget();
            Task.Run(() => _wjClient.SendMetric("started_sha", ThisAssembly.Git.Sha)).FireAndForget();

            try
            {
                var applicationRegistrationService = _serviceProvider.GetRequiredService<IApplicationRegistrationService>();

                var applicationInfo = new ApplicationInfo("Wabbajack", "Wabbajack", "Wabbajack", processLocation);
                applicationInfo.SupportedExtensions.Add("wabbajack");
                applicationRegistrationService.RegisterApplication(applicationInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "While setting up file associations");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "During App configuration");
            VersionDisplay = "ERROR";
        }
        CopyVersionCommand = ReactiveCommand.Create(() =>
        {
            Clipboard.SetText($"Wabbajack {VersionDisplay}\n{ThisAssembly.Git.Sha}");
        });
        GetHelpCommand = ReactiveCommand.Create(GetHelp);
        LoadLocalFileCommand = ReactiveCommand.Create(LoadLocalFile);
        MinimizeCommand = ReactiveCommand.Create(Minimize);
        MaximizeCommand = ReactiveCommand.Create(ToggleMaximized);
        CloseCommand = ReactiveCommand.Create(Close);
    }

    public void LoadModlist(AbsolutePath path)
    {
        LoadModlistForInstalling.Send(path, null);
        NavigateToGlobal.Send(ScreenType.Installer);
    }

    private void GetHelp()
    {
        if (ActivePane is ICanGetHelpVM) ((ICanGetHelpVM)ActivePane).GetHelpCommand.Execute(null);
    }

    private void LoadLocalFile()
    {
        if (ActivePane is ICanLoadLocalFileVM) ((ICanLoadLocalFileVM)ActivePane).LoadLocalFileCommand.Execute(null);
    }

    private void Minimize()
    {
        Application.Current.MainWindow.WindowState = WindowState.Minimized;
    }

    private void ToggleMaximized()
    {
        var currentWindowState = Application.Current.MainWindow.WindowState;
        var desiredWindowState = currentWindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

        /*
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.WindowState = desiredWindowState;
        */
        Application.Current.MainWindow.WindowState = desiredWindowState;
    }

    private void Close()
    {
        Environment.Exit(0);
    }

    private void HandleNavigateTo(ViewModel objViewModel)
    {
        ActivePane = objViewModel;
    }

    private void HandleManualDownload(ManualDownload manualDownload)
    {
        var handler = _serviceProvider.GetRequiredService<ManualDownloadHandler>();
        handler.Intervention = manualDownload;
        //MessageBus.Current.SendMessage(new OpenBrowserTab(handler));
    }

    private void HandleManualBlobDownload(ManualBlobDownload manualDownload)
    {
        var handler = _serviceProvider.GetRequiredService<ManualBlobDownloadHandler>();
        handler.Intervention = manualDownload;
        //MessageBus.Current.SendMessage(new OpenBrowserTab(handler));
    }

    private async void HandleShowBrowserWindow(ShowBrowserWindow msg)
    {
        using var _ = await _browserLocker.WaitAsync();
        var browserWindow = _serviceProvider.GetRequiredService<BrowserWindow>();
        ActiveFloatingPane = browserWindow.ViewModel = msg.ViewModel;
        browserWindow.DataContext = ActiveFloatingPane;
        await browserWindow.ViewModel.RunBrowserOperation();
    }

    private void HandleNavigateTo(ScreenType s)
    {
        ActivePane = s switch
        {
            ScreenType.Home => HomeVM,
            ScreenType.ModListGallery => GalleryVM,
            ScreenType.Installer => InstallerVM,
            ScreenType.CompilerHome => CompilerHomeVM,
            ScreenType.CompilerMain => CompilerMainVM,
            ScreenType.ModListDetails => ModListDetailsVM,
            ScreenType.Settings => SettingsPaneVM,
            ScreenType.Info => InfoVM,
            _ => ActivePane
        };
    }

    private void HandleShowFloatingWindow(FloatingScreenType s)
    {
        ActiveFloatingPane = s switch
        {
            FloatingScreenType.None => null,
            FloatingScreenType.ModListDetails => ModListDetailsVM,
            FloatingScreenType.FileUpload => FileUploadVM,
            FloatingScreenType.MegaLogin => MegaLoginVM,
            _ => ActiveFloatingPane
        };
    }

    private static bool IsStartingFromModlist(out AbsolutePath modlistPath)
    {
        var args = Environment.GetCommandLineArgs();
        if (args.Length == 2)
        {
            var arg = args[1].ToAbsolutePath();
            if (arg.FileExists() && arg.Extension == Ext.Wabbajack)
            {
                modlistPath = arg;
                return true;
            }
        }

        modlistPath = default;
        return false;
    }
    public void CancelRunningTasks(TimeSpan timeout)
    {
        var endTime = DateTime.Now.Add(timeout);
        var cancellationTokenSource = _serviceProvider.GetRequiredService<CancellationTokenSource>();
        cancellationTokenSource.Cancel();

        bool IsInstalling() => InstallerVM.InstallState is InstallState.Installing;

        while (DateTime.Now < endTime && IsInstalling())
        {
            Thread.Sleep(TimeSpan.FromSeconds(1));
        }
    }

    public async Task ShutdownApplication()
    {
        Dispose();
        /*
        Settings.PosX = MainWindow.Left;
        Settings.PosY = MainWindow.Top;
        Settings.Width = MainWindow.Width;
        Settings.Height = MainWindow.Height;
        await MainSettings.SaveSettings(Settings);
        Application.Current.Shutdown();
        */
    }
}
