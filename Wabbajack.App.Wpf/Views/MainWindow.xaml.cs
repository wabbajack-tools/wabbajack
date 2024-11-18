using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Input;
using DynamicData.Binding;
using MahApps.Metro.Controls;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using Wabbajack.Common;
using Wabbajack.Messages;
using Wabbajack.Paths.IO;
using Wabbajack.Util;

namespace Wabbajack
{
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        private MainWindowVM _mwvm;
        private readonly ILogger<MainWindow> _logger;
        private readonly SystemParametersConstructor _systemParams;

        private ObservableCollection<ViewModel> TabVMs = new ObservableCollectionExtended<ViewModel>();

        public MainWindow(ILogger<MainWindow> logger, SystemParametersConstructor systemParams, LauncherUpdater updater
            , MainWindowVM vm)
        {
            InitializeComponent();
            _mwvm = vm;
            DataContext = vm;
            _logger = logger;
            _systemParams = systemParams;
            try
            {
                // Wire any unhandled crashing exceptions to log before exiting
                AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
                {
                    // Don't do any special logging side effects
                    _logger.LogError((Exception)e.ExceptionObject, "Uncaught error");
                    Environment.Exit(-1);
                };

                Closed += (s, e) =>
                {
                    _logger.LogInformation("Beginning shutdown...");
                    _mwvm.CancelRunningTasks(TimeSpan.FromSeconds(10));
                    
                    // Cleaning the temp folder when the app closes since it can take up multiple Gigabytes of Storage
                    var tempDirectory = Environment.CurrentDirectory + "\\temp";
                    _logger.LogInformation("Clearing {TempDir}",tempDirectory);
                    var directoryInfo = new DirectoryInfo(tempDirectory);
                    try
                    {
                        foreach (var file in directoryInfo.EnumerateFiles())
                        {
                            file.Delete();
                        }

                        foreach (var dir in directoryInfo.EnumerateDirectories())
                        {
                            dir.Delete(true);
                        }

                        _logger.LogInformation("Finished clearing {TempDir}", tempDirectory);
                    }
                    catch (DirectoryNotFoundException)
                    {
                        _logger.LogInformation("Unable to find {TempDir}", tempDirectory);
                    }
                    
                    Application.Current.Shutdown();
                };

                MessageBus.Current.Listen<TaskBarUpdate>()
                    .ObserveOnGuiThread()
                    .Subscribe(u =>
                    {
                        TaskbarItemInfoControl.Description = u.Description;
                        TaskbarItemInfoControl.ProgressValue = u.ProgressValue;
                        TaskbarItemInfoControl.ProgressState = u.State;
                    });


                _logger.LogInformation("Wabbajack Build - {Sha}",ThisAssembly.Git.Sha);
                _logger.LogInformation("Running in {EntryPoint}", KnownFolders.EntryPoint);

                var p = _systemParams.Create();

                _logger.LogInformation("Detected Windows Version: {Version}", Environment.OSVersion.VersionString);

                _logger.LogInformation(
                    "System settings - ({MemorySize} RAM) ({PageSize} Page), Display: {ScreenWidth} x {ScreenHeight} ({Vram} VRAM - VideoMemorySizeMb={ENBVRam})",
                    p.SystemMemorySize.ToFileSizeString(), p.SystemPageSize.ToFileSizeString(), p.ScreenWidth, p.ScreenHeight, p.VideoMemorySize.ToFileSizeString(), p.EnbLEVRAMSize);

                if (p.SystemPageSize == 0)
                    _logger.LogInformation( "Pagefile is disabled! Consider increasing to 20000MB. A disabled pagefile can cause crashes and poor in-game performance");
                else if (p.SystemPageSize < 2e+10)
                    _logger.LogInformation("Pagefile below recommended! Consider increasing to 20000MB. A suboptimal pagefile can cause crashes and poor in-game performance");

                var _ = updater.Run();

                // Bring window to the front if it isn't already
                this.Initialized += (s, e) =>
                {
                    this.Activate();
                    this.Topmost = true;
                    this.Focus();
                };
                this.ContentRendered += (s, e) =>
                {
                    this.Topmost = false;
                };

                ((MainWindowVM) DataContext).WhenAnyValue(vm => vm.OpenSettingsCommand)
                    .BindTo(this, view => view.SettingsButton.Command);

                ((MainWindowVM)DataContext).WhenAnyValue(vm => vm.Installer.InstallState)
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Select(v => v == InstallState.Installing ? Visibility.Collapsed : Visibility.Visible)
                    .BindTo(this, view => view.SettingsButton.Visibility);
                
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "During Main Window Startup");
                Environment.Exit(-1);
            }

            vm.WhenAnyValue(vm => vm.ResourceStatus)
                .BindToStrict(this, view => view.ResourceUsage.Text);
            vm.WhenAnyValue(vm => vm.AppName)
                .BindToStrict(this, view => view.AppName.Text);

        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            _mwvm.ShutdownApplication().Wait();
        }

        private void UIElement_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

    }
}
