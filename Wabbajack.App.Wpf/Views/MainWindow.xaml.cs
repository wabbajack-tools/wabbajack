using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Reactive.Disposables;
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

namespace Wabbajack;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : MetroWindow
{
    private MainWindowVM _mwvm;
    private readonly ILogger<MainWindow> _logger;
    private readonly SystemParametersConstructor _systemParams;

    public MainWindow(ILogger<MainWindow> logger, SystemParametersConstructor systemParams, LauncherUpdater updater, MainWindowVM vm)
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
                _logger.LogInformation("Pagefile is disabled! This will cause issues such as crashing with Wabbajack and other applications!");

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

            vm.WhenAnyValue(vm => vm.ActivePane)
                .Subscribe(pane => WizardSteps.Visibility = (pane is IWizardVM) ? Visibility.Visible : Visibility.Collapsed);

            vm.WhenAnyValue(vm => vm.ActivePane)
              .Where(pane => pane is IWizardVM)
              .Subscribe(pane =>
              {
                  var wizardVM = (WizardViewModel)pane;

                  wizardVM.WhenAnyValue(x => x.ConfigurationText)
                          .BindTo(this, view => view.WizardConfigurationButton.Content)
                          .DisposeWith(wizardVM.CompositeDisposable);
                  wizardVM.WhenAnyValue(x => x.ProgressText)
                          .BindTo(this, view => view.ProgressText.Text)
                          .DisposeWith(wizardVM.CompositeDisposable);
                  wizardVM.WhenAnyValue(x => x.ProgressPercent.Value)
                          .Select(x => x.IsGreaterThan(0) ? Visibility.Visible : Visibility.Hidden)
                          .BindTo(this, view => view.ProgressPercentage.Visibility)
                          .DisposeWith(wizardVM.CompositeDisposable);
                  wizardVM.WhenAnyValue(x => x.ProgressPercent.Value)
                          .Select(x => (int)(x * 100) + "%")
                          .BindTo(this, view => view.ProgressPercentage.Text)
                          .DisposeWith(wizardVM.CompositeDisposable);
                  wizardVM.WhenAnyValue(x => x.ProgressPercent.Value)
                          .BindTo(this, view => view.WizardProgressBar.Value)
                          .DisposeWith(wizardVM.CompositeDisposable);
                  wizardVM.WhenAnyValue(x => x.CurrentStep)
                          .Subscribe(step =>
                          {
                              WizardConfigurationButton.Width = double.NaN;
                              WizardConfigurationButton.HorizontalContentAlignment = HorizontalAlignment.Center;
                              WizardProgressBar.Width = double.NaN;
                              ProgressText.HorizontalAlignment = HorizontalAlignment.Center;
                              if (step == Step.Configuration)
                              {
                                  WizardConfigurationButton.Width = 500;
                                  WizardConfigurationButton.HorizontalContentAlignment = HorizontalAlignment.Left;
                              }
                              else if (step == Step.Busy)
                              {
                                  WizardProgressBar.Width = 500;
                                  ProgressText.HorizontalAlignment = HorizontalAlignment.Left;
                              }
                          })
                          .DisposeWith(wizardVM.CompositeDisposable);

              });

            vm.WhenAnyValue(vm => vm.ActivePane)
                .Subscribe(pane => InfoButton.Visibility = (pane is IHasInfoVM) ? Visibility.Visible : Visibility.Collapsed);
            vm.WhenAnyValue(vm => vm.InfoCommand)
                .BindTo(this, view => view.InfoButton.Command);

            vm.WhenAnyValue(vm => vm.MinimizeCommand)
                .BindTo(this, view => view.MinimizeButton.Command);

            vm.WhenAnyValue(vm => vm.MaximizeCommand)
                .BindTo(this, view => view.MaximizeButton.Command);

            vm.WhenAnyValue(vm => vm.CloseCommand)
                .BindTo(this, view => view.CloseButton.Command);

            /*
            ((MainWindowVM)DataContext).WhenAnyValue(vm => vm.Installer.InstallState)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Select(v => v == InstallState.Installing ? Visibility.Collapsed : Visibility.Visible)
                .BindTo(this, view => view.SettingsButton.Visibility);
            */

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "During Main Window Startup");
            Environment.Exit(-1);
        }

        /*
        vm.WhenAnyValue(vm => vm.ResourceStatus)
            .BindToStrict(this, view => view.ResourceUsage.Text);
        */
        vm.WhenAnyValue(vm => vm.WindowTitle)
            .BindToStrict(this, view => view.AppName.Text);

    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        _mwvm.ShutdownApplication().Wait();
    }

    private void UIElement_OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        this.DragMove();
    }

}
