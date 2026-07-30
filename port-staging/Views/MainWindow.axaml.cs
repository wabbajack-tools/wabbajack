using System;
using System.Diagnostics;
using System.IO;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using Wabbajack.Common;
using Wabbajack.Messages;

namespace Wabbajack;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
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
                _logger.LogInformation("Clearing {TempDir}", tempDirectory);
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

                // TODO: WPF's Application.Current.Shutdown() has no direct Avalonia equivalent; shutting
                // down the classic desktop application lifetime is the closest match.
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    desktop.Shutdown();
                }
            };

            var _ = updater.Run();

            // Bring window to the front if it isn't already.
            // TODO: WPF's Window.Initialized/ContentRendered events have no exact Avalonia equivalent.
            // Opened fires once the window has been shown; Topmost is reset shortly afterwards via the
            // dispatcher to approximate the original "flash to front, then stop being topmost" behavior.
            this.Opened += (s, e) =>
            {
                this.Activate();
                this.Topmost = true;
                this.Focus();
                Dispatcher.UIThread.Post(() => this.Topmost = false, DispatcherPriority.Background);
            };

            vm.WhenAnyValue(vm => vm.ActivePane)
                .Subscribe(pane => WizardSteps.IsVisible = pane is IProgressVM);

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
                  // TODO: WPF's Visibility.Hidden keeps layout space reserved while invisible; Avalonia's
                  // IsVisible=false behaves like Collapsed (layout space is not reserved). No direct
                  // equivalent exists without a custom layout workaround.
                  wizardVM.WhenAnyValue(x => x.ProgressPercent.Value)
                          .Select(x => x.IsGreaterThan(0) && !x.IsOne())
                          .BindTo(this, view => view.ProgressPercentage.IsVisible)
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
                .Subscribe(pane => GetHelpButton.IsVisible = pane is ICanGetHelpVM);
            vm.WhenAnyValue(vm => vm.GetHelpCommand)
                .BindTo(this, view => view.GetHelpButton.Command);

            vm.WhenAnyValue(vm => vm.ActivePane)
                .Subscribe(pane => LoadLocalFileButton.IsVisible = pane is ICanLoadLocalFileVM);
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

            // TODO: The original used ReactiveMarbles.ObservableEvents' `.Events()` source-generated
            // observables (TitleBar.Events().MouseDown / this.Events().KeyDown) for WPF controls. Avalonia
            // support for that generator was not confirmed in this project, so these were converted to
            // plain CLR event subscriptions instead; behavior is equivalent.
            TitleBar.PointerPressed += TitleBar_OnPointerPressed;

            FloatingWindowBackground.PointerPressed += FloatingWindowBackground_OnPointerPressed;
            FloatingWindowBackground.PointerReleased += FloatingWindowBackground_OnPointerReleased;

            vm.WhenAnyValue(vm => vm.ActiveFloatingPane)
                .Select(x => x != null)
                .BindTo(this, view => view.FloatingWindow.IsVisible);

            // Hide main content when floating pane is active to prevent WebView2 airspace overlap
            vm.WhenAnyValue(vm => vm.ActiveFloatingPane)
                .Select(x => x == null)
                .BindTo(this, view => view.MainContent.IsVisible);

            this.KeyDown += HandleKeyDown;

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "During Main Window Startup");
            Environment.Exit(-1);
        }

        vm.WhenAnyValue(vm => vm.WindowTitle)
          .BindToStrict(this, view => view.AppName.Text);
    }

    private void Window_Closing(object sender, WindowClosingEventArgs e)
    {
        _mwvm.ShutdownApplication().Wait();
    }

    private void TitleBar_OnPointerPressed(object sender, PointerPressedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
            WindowState = WindowState.Normal;

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void FloatingWindowBackground_OnPointerPressed(object sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(FloatingWindowBackground).Properties.IsLeftButtonPressed)
        {
            _mousePressedTimer.Restart();
            BeginMoveDrag(e);
        }
    }

    private void FloatingWindowBackground_OnPointerReleased(object sender, PointerReleasedEventArgs e)
    {
        if (_mousePressedTimer.Elapsed < TimeSpan.FromSeconds(0.2))
        {
            if (((MainWindowVM)DataContext).ActiveFloatingPane is IClosableVM closingPane)
                closingPane.CloseCommand.Execute(null);
            else
                ShowFloatingWindow.Send(FloatingScreenType.None);
        }
        _mousePressedTimer.Stop();
    }

    private void HandleKeyDown(object sender, KeyEventArgs x)
    {
        if (x.Key == Key.Escape)
        {
            if (((MainWindowVM)DataContext).ActiveFloatingPane is IClosableVM closingPane)
                closingPane.CloseCommand.Execute(null);
            else
                ShowFloatingWindow.Send(FloatingScreenType.None);
        }
    }

}
