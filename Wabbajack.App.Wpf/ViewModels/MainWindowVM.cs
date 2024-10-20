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

namespace Wabbajack;

/// <summary>
/// Main View Model for the application.
/// Keeps track of which sub view is being shown in the window, and has some singleton wiring like WorkQueue and Logging.
/// </summary>
public class MainWindowVM : ViewModel
{
    public MainWindow MainWindow { get; }

    [Reactive]
    public ViewModel ActivePane { get; private set; }

    [Reactive]
    public NavigationVM NavigationVM { get; private set; }

    public ObservableCollectionExtended<IStatusMessage> Log { get; } = new ObservableCollectionExtended<IStatusMessage>();

    public readonly CompilerHomeVM CompilerHomeVM;
    public readonly CompilerDetailsVM CompilerDetailsVM;
    public readonly CompilerFileManagerVM CompilerFileManagerVM;
    public readonly CompilerMainVM CompilerMainVM;
    public readonly InstallerVM InstallerVM;
    public readonly SettingsVM SettingsPaneVM;
    public readonly ModListGalleryVM GalleryVM;
    public readonly HomeVM HomeVM;
    public readonly WebBrowserVM WebBrowserVM;
    public readonly ModListDetailsVM ModListDetailsVM;
    public readonly InfoVM InfoVM;
    public readonly UserInterventionHandlers UserInterventionHandlers;
    private readonly Client _wjClient;
    private readonly ILogger<MainWindowVM> _logger;
    private readonly ResourceMonitor _resourceMonitor;

    private List<ViewModel> PreviousPanes = new();
    private readonly IServiceProvider _serviceProvider;

    public ICommand CopyVersionCommand { get; }
    public ICommand ShowLoginManagerVM { get; }
    public ICommand InfoCommand { get; }
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
        InstallerVM installerVM, CompilerHomeVM compilerHomeVM, CompilerDetailsVM compilerDetailsVM, CompilerFileManagerVM compilerFileManagerVM, CompilerMainVM compilerMainVM, SettingsVM settingsVM, WebBrowserVM webBrowserVM, NavigationVM navigationVM, InfoVM infoVM, ModListDetailsVM modlistDetailsVM)
    {
        _logger = logger;
        _wjClient = wjClient;
        _resourceMonitor = resourceMonitor;
        _serviceProvider = serviceProvider;
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
        ModListDetailsVM = modlistDetailsVM;
        InfoVM = infoVM;
        UserInterventionHandlers = new UserInterventionHandlers(serviceProvider.GetRequiredService<ILogger<UserInterventionHandlers>>(), this);

        MessageBus.Current.Listen<NavigateToGlobal>()
            .Subscribe(m => HandleNavigateTo(m.Screen))
            .DisposeWith(CompositeDisposable);

        MessageBus.Current.Listen<NavigateTo>()
            .Subscribe(m => HandleNavigateTo(m.ViewModel))
            .DisposeWith(CompositeDisposable);

        MessageBus.Current.Listen<NavigateBack>()
            .Subscribe(HandleNavigateBack)
            .DisposeWith(CompositeDisposable);

        MessageBus.Current.Listen<SpawnBrowserWindow>()
            .ObserveOnGuiThread()
            .Subscribe(HandleSpawnBrowserWindow)
            .DisposeWith(CompositeDisposable);

        MessageBus.Current.Listen<ShowNavigation>()
            .ObserveOnGuiThread()
            .Subscribe((_) => NavigationVisible = true)
            .DisposeWith(CompositeDisposable);

        MessageBus.Current.Listen<HideNavigation>()
            .ObserveOnGuiThread()
            .Subscribe((_) => NavigationVisible = false)
            .DisposeWith(CompositeDisposable);

        _resourceMonitor.Updates
            .Select(r => string.Join(", ", r.Where(r => r.Throughput > 0)
            .Select(s => $"{s.Name} - {s.Throughput.ToFileSizeString()}/s")))
            .BindToStrict(this, view => view.ResourceStatus);


        if (IsStartingFromModlist(out var path))
        {
            LoadModlistForInstalling.Send(path, null);
            NavigateToGlobal.Send(ScreenType.Installer);
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

            _logger.LogInformation("Assembly Location: {AssemblyLocation}", assemblyLocation);
            _logger.LogInformation("Process Location: {ProcessLocation}", processLocation);

            var fvi = FileVersionInfo.GetVersionInfo(string.IsNullOrWhiteSpace(assemblyLocation) ? processLocation : assemblyLocation);
            Consts.CurrentMinimumWabbajackVersion = Version.Parse(fvi.FileVersion);
            WindowTitle = Consts.AppName;
            _logger.LogInformation("Wabbajack Version: {FileVersion}", fvi.FileVersion);

            Task.Run(() => _wjClient.SendMetric("started_wabbajack", fvi.FileVersion)).FireAndForget();
            Task.Run(() => _wjClient.SendMetric("started_sha", ThisAssembly.Git.Sha));

            // setup file association
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
        InfoCommand = ReactiveCommand.Create(ShowInfo);
        MinimizeCommand = ReactiveCommand.Create(Minimize);
        MaximizeCommand = ReactiveCommand.Create(Maximize);
        CloseCommand = ReactiveCommand.Create(Close);
    }

    private void ShowInfo()
    {
        if (ActivePane is IHasInfoVM) ((IHasInfoVM)ActivePane).InfoCommand.Execute(null);
    }

    private void Minimize()
    {
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.WindowState = WindowState.Minimized;
    }

    private void Maximize()
    {
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.WindowState = WindowState.Maximized;
    }

    private void Close()
    {
        Environment.Exit(0);
    }

    private void HandleNavigateTo(ViewModel objViewModel)
    {
        ActivePane = objViewModel;
    }

    private void HandleNavigateBack(NavigateBack navigateBack)
    {
        ActivePane = PreviousPanes.Last();
        PreviousPanes.RemoveAt(PreviousPanes.Count - 1);
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

    private void HandleSpawnBrowserWindow(SpawnBrowserWindow msg)
    {
        var window = _serviceProvider.GetRequiredService<BrowserWindow>();
        window.DataContext = msg.Vm;
        window.Show();
    }

    private void HandleNavigateTo(ScreenType s)
    {
        if (s is ScreenType.Settings)
            PreviousPanes.Add(ActivePane);

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
        /*
        Dispose();
        Settings.PosX = MainWindow.Left;
        Settings.PosY = MainWindow.Top;
        Settings.Width = MainWindow.Width;
        Settings.Height = MainWindow.Height;
        await MainSettings.SaveSettings(Settings);
        Application.Current.Shutdown();
        */
    }
}
