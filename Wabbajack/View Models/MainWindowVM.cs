using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Alphaleonis.Win32.Filesystem;
using Wabbajack.Common;
using Wabbajack.Common.StatusFeed;
using Wabbajack.Lib;

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
        public readonly Lazy<ModListGalleryVM> Gallery;
        public readonly ModeSelectionVM ModeSelectionVM;
        public readonly UserInterventionHandlers UserInterventionHandlers;

        public ICommand CopyVersionCommand { get; }
        public ICommand ShowLoginManagerVM { get; }
        public ICommand OpenSettingsCommand { get; }
        public ICommand OpenTerminalCommand { get; }

        public string VersionDisplay { get; }

        [Reactive]
        public bool UpdateAvailable { get; private set; }

        public MainWindowVM(MainWindow mainWindow, MainSettings settings)
        {
            ConverterRegistration.Register();
            MainWindow = mainWindow;
            Settings = settings;
            Installer = new Lazy<InstallerVM>(() => new InstallerVM(this));
            Compiler = new Lazy<CompilerVM>(() => new CompilerVM(this));
            SettingsPane = new Lazy<SettingsVM>(() => new SettingsVM(this));
            Gallery = new Lazy<ModListGalleryVM>(() => new ModListGalleryVM(this));
            ModeSelectionVM = new ModeSelectionVM(this);
            UserInterventionHandlers = new UserInterventionHandlers(this);

            // Set up logging
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
            
            var singleton_lock = new AsyncLock();

            Utils.LogMessages
                .OfType<IUserIntervention>()
                .ObserveOnGuiThread()
                .SelectTask(async msg =>
                {
                    using var _ = await singleton_lock.WaitAsync();
                    try
                    {
                        await UserInterventionHandlers.Handle(msg);
                    }
                    catch (Exception ex)
                        when (ex.GetType() != typeof(TaskCanceledException))
                    {
                        Utils.Error(ex, $"Error while handling user intervention of type {msg?.GetType()}");
                        try
                        {
                            if (!msg.Handled)
                            {
                                msg.Cancel();
                            }
                        }
                        catch (Exception cancelEx)
                        {
                            Utils.Error(cancelEx, $"Error while cancelling user intervention of type {msg?.GetType()}");
                        }
                    }
                })
                .Subscribe()
                .DisposeWith(CompositeDisposable);

            if (IsStartingFromModlist(out var path))
            {
                Installer.Value.ModListLocation.TargetPath = path;
                NavigateTo(Installer.Value);
            }
            else
            {
                // Start on mode selection
                NavigateTo(ModeSelectionVM);
            }

            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
                VersionDisplay = $"v{fvi.FileVersion}";
                Utils.Log($"Wabbajack Version: {fvi.FileVersion}");
            }
            catch (Exception ex)
            {
                Utils.Error(ex);
                VersionDisplay = "ERROR";
            }
            CopyVersionCommand = ReactiveCommand.Create(() =>
            {
                Clipboard.SetText($"Wabbajack {VersionDisplay}\n{ThisAssembly.Git.Sha}");
            });
            OpenSettingsCommand = ReactiveCommand.Create(
                canExecute: this.WhenAny(x => x.ActivePane)
                    .Select(active => !SettingsPane.IsValueCreated || !object.ReferenceEquals(active, SettingsPane.Value)),
                execute: () => NavigateTo(SettingsPane.Value));

            OpenTerminalCommand = ReactiveCommand.Create(() => OpenTerminal());
        }

        private void OpenTerminal()
        {
            var process = new ProcessStartInfo
            {
                FileName = "cmd.exe", 
                WorkingDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)
            };
            Process.Start(process);
        }

        private static bool IsStartingFromModlist(out AbsolutePath modlistPath)
        {
            if (CLIArguments.InstallPath == null)
            {
                modlistPath = default;
                return false;
            }

            modlistPath = (AbsolutePath)CLIArguments.InstallPath;
            return true;
        }


        public void OpenInstaller(AbsolutePath path)
        {
            if (path == default) return;
            var installer = Installer.Value;
            Settings.Installer.LastInstalledListLocation = path;
            NavigateTo(installer);
            installer.ModListLocation.TargetPath = path;
        }

        public void NavigateTo(ViewModel vm)
        {
            ActivePane = vm;
        }

        public void NavigateTo<T>(T vm)
            where T : ViewModel, IBackNavigatingVM
        {
            vm.NavigateBackTarget = ActivePane;
            ActivePane = vm;
        }

        public void ShutdownApplication()
        {
            Dispose();
            Settings.PosX = MainWindow.Left;
            Settings.PosY = MainWindow.Top;
            Settings.Width = MainWindow.Width;
            Settings.Height = MainWindow.Height;
            MainSettings.SaveSettings(Settings);
            Application.Current.Shutdown();
        }
    }
}
