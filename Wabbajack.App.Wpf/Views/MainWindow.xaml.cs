using System;
using System.ComponentModel;
using System.IO;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Input;
using MahApps.Metro.Controls;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using Wabbajack.Common;
using Wabbajack.Messages;
using ReactiveMarbles.ObservableEvents;
using System.Diagnostics;
using System.Linq;

namespace Wabbajack;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : MetroWindow
{
    private MainWindowVM _mwvm;
    private readonly ILogger<MainWindow> _logger;
    private readonly Stopwatch _mousePressedTimer;

    public MainWindow(ILogger<MainWindow> logger, LauncherUpdater updater, MainWindowVM vm)
    {
        InitializeComponent();
        _mwvm = vm;
        DataContext = vm;
        _logger = logger;
        _mousePressedTimer = new Stopwatch();

        try
        {
            // Wire any unhandled crashing exceptions to log before exiting
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                // Don't do any special logging side effects
                _logger.LogError((Exception)e.ExceptionObject, "Uncaught error");
                throw (Exception)e.ExceptionObject;
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
                .Subscribe(pane => WizardSteps.Visibility = (pane is IProgressVM) ? Visibility.Visible : Visibility.Collapsed);

            vm.WhenAnyValue(vm => vm.ActivePane)
              .Where(pane => pane is IProgressVM)
              .Subscribe(pane =>
              {
                  var wizardVM = (ProgressViewModel)pane;

                  wizardVM.WhenAnyValue(x => x.ConfigurationText)
                          .BindTo(this, view => view.ConfigurationText.Text)
                          .DisposeWith(wizardVM.CompositeDisposable);
                  wizardVM.WhenAnyValue(x => x.ProgressText)
                          .BindTo(this, view => view.ProgressText.Text)
                          .DisposeWith(wizardVM.CompositeDisposable);
                  wizardVM.WhenAnyValue(x => x.ProgressPercent.Value)
                          .Select(x => x.IsGreaterThan(0) && !x.IsOne() ? Visibility.Visible : Visibility.Hidden)
                          .BindTo(this, view => view.ProgressPercentage.Visibility)
                          .DisposeWith(wizardVM.CompositeDisposable);
                  wizardVM.WhenAnyValue(x => x.ProgressPercent.Value)
                          .Select(x => (int)(x * 100) + "%")
                          .BindTo(this, view => view.ProgressPercentage.Text)
                          .DisposeWith(wizardVM.CompositeDisposable);
                  wizardVM.WhenAnyValue(x => x.ProgressPercent.Value)
                          .BindTo(this, view => view.WizardProgress.Value)
                          .DisposeWith(wizardVM.CompositeDisposable);
                  wizardVM.WhenAnyValue(x => x.CurrentStep)
                          .ObserveOnGuiThread()
                          .Subscribe(step =>
                          {
                              ConfigurationText.Width = double.NaN;
                              ConfigurationText.HorizontalAlignment = HorizontalAlignment.Center;
                              ProgressText.Width = double.NaN;
                              ProgressText.HorizontalAlignment = HorizontalAlignment.Center;
                              if (step == Step.Configuration)
                              {
                                  ConfigurationText.Width = 500;
                                  ConfigurationText.HorizontalAlignment = HorizontalAlignment.Left;
                              }
                              else if (step == Step.Busy)
                              {
                                  ProgressText.Width = 500;
                                  ProgressText.HorizontalAlignment = HorizontalAlignment.Left;
                              }
                          })
                          .DisposeWith(wizardVM.CompositeDisposable);

              });

            vm.WhenAnyValue(vm => vm.ActivePane)
                .Subscribe(pane => GetHelpButton.Visibility = (pane is ICanGetHelpVM) ? Visibility.Visible : Visibility.Collapsed);
            vm.WhenAnyValue(vm => vm.GetHelpCommand)
                .BindTo(this, view => view.GetHelpButton.Command);

            vm.WhenAnyValue(vm => vm.ActivePane)
                .Subscribe(pane => LoadLocalFileButton.Visibility = (pane is ICanLoadLocalFileVM) ? Visibility.Visible : Visibility.Collapsed);
            vm.WhenAnyValue(vm => vm.LoadLocalFileCommand)
                .BindTo(this, view => view.LoadLocalFileButton.Command);

            vm.WhenAnyValue(vm => vm.MinimizeCommand)
                .BindTo(this, view => view.MinimizeButton.Command);

            vm.WhenAnyValue(vm => vm.MaximizeCommand)
                .BindTo(this, view => view.MaximizeButton.Command);

            vm.WhenAnyValue(vm => vm.CloseCommand)
                .BindTo(this, view => view.CloseButton.Command);

            vm.WhenAnyValue(vm => vm.NavigationVisible)
                .Subscribe(v => NavigationColumn.Width = v ? new GridLength(115, GridUnitType.Pixel) : new GridLength(0, GridUnitType.Pixel));

            TitleBar.Events().MouseDown
                .Subscribe(x => TitleBar_OnMouseDown(this, x));

            FloatingWindowBackground.Events().MouseDown
                .Subscribe(x => FloatingWindowBackground_OnMouseDown(this, x));

            vm.WhenAnyValue(vm => vm.ActiveFloatingPane)
                .Select(x => x == null ? Visibility.Hidden : Visibility.Visible)
                .BindTo(this, view => view.FloatingWindow.Visibility);

            this.Events().KeyDown
                .Subscribe(x => HandleKeyDown(this, x));

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "During Main Window Startup");
            Environment.Exit(-1);
        }

        vm.WhenAnyValue(vm => vm.WindowTitle)
          .BindToStrict(this, view => view.AppName.Text);
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        _mwvm.ShutdownApplication().Wait();
    }

    private void TitleBar_OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (Application.Current.MainWindow.WindowState == WindowState.Maximized)
            Application.Current.MainWindow.WindowState = WindowState.Normal;

        DragMove();
    }

    private void FloatingWindowBackground_OnMouseDown(object sender, MouseButtonEventArgs x)
    {
        if (x.ButtonState == MouseButtonState.Pressed)
        {
            _mousePressedTimer.Restart();
            DragMove();
        }
        if(x.ButtonState == MouseButtonState.Released)
        {
            if(_mousePressedTimer.Elapsed < TimeSpan.FromSeconds(0.2))
            {
                if (((MainWindowVM)DataContext).ActiveFloatingPane is IClosableVM closingPane)
                    closingPane.CloseCommand.Execute(null);
                else
                    ShowFloatingWindow.Send(FloatingScreenType.None);
            }
            _mousePressedTimer.Stop();
        }
    }

    private void HandleKeyDown(MainWindow mainWindow, KeyEventArgs x)
    {
        if(x.Key == Key.Escape)
        {
            if (((MainWindowVM)DataContext).ActiveFloatingPane is IClosableVM closingPane)
                closingPane.CloseCommand.Execute(null);
            else
                ShowFloatingWindow.Send(FloatingScreenType.None);
        }
    }

}
