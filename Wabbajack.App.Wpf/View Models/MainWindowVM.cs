using DynamicData.Binding;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Diagnostics;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wabbajack.Common;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.Lib;
using Wabbajack.Lib.Interventions;
using Wabbajack.Messages;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Paths;
using Wabbajack.View_Models;

namespace Wabbajack
{
    /// <summary>
    /// Main View Model for the application.
    /// Keeps track of which sub view is being shown in the window, and has some singleton wiring like WorkQueue and Logging.
    /// </summary>
    public class MainWindowVM : ViewModel
    {
        public MainWindow MainWindow { get; }

        public MainSettings Settings { get; }

        [Reactive]
        public ViewModel ActivePane { get; private set; }

        public ObservableCollectionExtended<IStatusMessage> Log { get; } = new ObservableCollectionExtended<IStatusMessage>();

        public readonly Lazy<CompilerVM> Compiler;
        public readonly Lazy<InstallerVM> Installer;
        public readonly Lazy<SettingsVM> SettingsPane;
        public readonly ModListGalleryVM Gallery;
        public readonly ModeSelectionVM ModeSelectionVM;
        public readonly Lazy<ModListContentsVM> ModListContentsVM;
        public readonly UserInterventionHandlers UserInterventionHandlers;
        private readonly Client _wjClient;
        private readonly ILogger<MainWindowVM> _logger;

        public ICommand CopyVersionCommand { get; }
        public ICommand ShowLoginManagerVM { get; }
        public ICommand OpenSettingsCommand { get; }

        public string VersionDisplay { get; }

        [Reactive]
        public bool UpdateAvailable { get; private set; }

        public MainWindowVM(ILogger<MainWindowVM> logger, MainSettings settings, Client wjClient,
            IServiceProvider serviceProvider, ModeSelectionVM modeSelectionVM, ModListGalleryVM modListGalleryVM)
        {
            _logger = logger;
            _wjClient = wjClient;
            ConverterRegistration.Register();
            Settings = settings;
            Installer = new Lazy<InstallerVM>(() => new InstallerVM(serviceProvider.GetRequiredService<ILogger<InstallerVM>>(), this, serviceProvider));
            Compiler = new Lazy<CompilerVM>(() => new CompilerVM(serviceProvider.GetRequiredService<ILogger<CompilerVM>>(), this));
            SettingsPane = new Lazy<SettingsVM>(() => new SettingsVM(serviceProvider.GetRequiredService<ILogger<SettingsVM>>(), this, serviceProvider));
            Gallery = modListGalleryVM;
            ModeSelectionVM = modeSelectionVM;
            ModListContentsVM = new Lazy<ModListContentsVM>(() => new ModListContentsVM(serviceProvider.GetRequiredService<ILogger<ModListContentsVM>>(), this));
            UserInterventionHandlers = new UserInterventionHandlers(serviceProvider.GetRequiredService<ILogger<UserInterventionHandlers>>(), this);

            MessageBus.Current.Listen<NavigateToGlobal>()
                .Subscribe(m => HandleNavigateTo(m.Screen))
                .DisposeWith(CompositeDisposable);
            
            // Set up logging
            /* TODO
            Utils.LogMessages
                .ObserveOn(RxApp.TaskpoolScheduler)
                .ToObservableChangeSet()
                .Buffer(TimeSpan.FromMilliseconds(250), RxApp.TaskpoolScheduler)
                .Where(l => l.Count > 0)
                .FlattenBufferResult()
                .ObserveOnGuiThread()
                .Bind(Log)
                .Subscribe()
                .DisposeWith(CompositeDisposable);

            Utils.LogMessages
                .Where(a => a is IUserIntervention or CriticalFailureIntervention)
                .ObserveOnGuiThread()
                .SelectTask(async msg =>
                {
                    try
                    {
                        await UserInterventionHandlers.Handle(msg);
                    }
                    catch (Exception ex)
                        when (ex.GetType() != typeof(TaskCanceledException))
                    {
                        _logger.LogError(ex, "Error while handling user intervention of type {Type}",msg?.GetType());
                        try
                        {
                            if (msg is IUserIntervention {Handled: false} intervention)
                            {
                                intervention.Cancel();
                            }
                        }
                        catch (Exception cancelEx)
                        {
                            _logger.LogError(cancelEx, "Error while cancelling user intervention of type {Type}",msg?.GetType());
                        }
                    }
                })
                .Subscribe()
                .DisposeWith(CompositeDisposable);
                */

            if (IsStartingFromModlist(out var path))
            {
                Installer.Value.ModListLocation.TargetPath = path;
                NavigateToGlobal.Send(NavigateToGlobal.ScreenType.Installer);
            }
            else
            {
                // Start on mode selection
                NavigateToGlobal.Send(NavigateToGlobal.ScreenType.ModeSelectionView);
            }

            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
                Consts.CurrentMinimumWabbajackVersion = Version.Parse(fvi.FileVersion);
                VersionDisplay = $"v{fvi.FileVersion}";
                _logger.LogInformation("Wabbajack Version: {FileVersion}", fvi.FileVersion);
                
                Task.Run(() => _wjClient.SendMetric("started_wabbajack", fvi.FileVersion)).FireAndForget();
                Task.Run(() => _wjClient.SendMetric("started_sha", ThisAssembly.Git.Sha));
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
            OpenSettingsCommand = ReactiveCommand.Create(
                canExecute: this.WhenAny(x => x.ActivePane)
                    .Select(active => !SettingsPane.IsValueCreated || !object.ReferenceEquals(active, SettingsPane.Value)),
                execute: () => NavigateToGlobal.Send(NavigateToGlobal.ScreenType.Settings));
        }

        private void HandleNavigateTo(NavigateToGlobal.ScreenType s)
        {
            ActivePane = s switch
            {
                NavigateToGlobal.ScreenType.ModeSelectionView => ModeSelectionVM,
                NavigateToGlobal.ScreenType.ModListGallery => Gallery,
                NavigateToGlobal.ScreenType.Installer => Installer.Value,
                NavigateToGlobal.ScreenType.Settings => SettingsPane.Value,
                _ => ActivePane
            };
        }

        private static bool IsStartingFromModlist(out AbsolutePath modlistPath)
        {
            /* TODO
            if (CLIArguments.InstallPath == null)
            {
                modlistPath = default;
                return false;
            }

            modlistPath = (AbsolutePath)CLIArguments.InstallPath;
            return true;
            */
            modlistPath = default;
            return false;
        }

        public void OpenInstaller(AbsolutePath path)
        {
            if (path == default) return;
            var installer = Installer.Value;
            Settings.Installer.LastInstalledListLocation = path;
            NavigateToGlobal.Send(NavigateToGlobal.ScreenType.Installer);
            installer.ModListLocation.TargetPath = path;
        }

        /*
        public void NavigateTo(ViewModel vm)
        {
            ActivePane = vm;
        }*/

        /*
        public void NavigateTo<T>(T vm)
            where T : ViewModel, IBackNavigatingVM
        {
            vm.NavigateBackTarget = ActivePane;
            ActivePane = vm;
        }*/

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
}
