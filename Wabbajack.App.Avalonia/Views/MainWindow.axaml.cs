using System;
using System.Diagnostics;
using System.IO;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using Wabbajack.App.Avalonia.ViewModels;

namespace Wabbajack.App.Avalonia.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        TitleBar.PointerPressed += TitleBar_PointerPressed;
        FloatingWindowBackground.PointerPressed += FloatingWindowBackground_PointerPressed;
        KeyDown += OnKeyDown;
        Closed += (_, _) => OnClosed();

        // As in WPF: an exception nothing caught is written to the log before the process goes down.
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Logger.LogError((Exception)e.ExceptionObject, "Uncaught error");
        DataContextChanged += (_, _) => WatchViewModel();
    }

    private MainWindowVM? VM => DataContext as MainWindowVM;

    private static ILogger<MainWindow> Logger => Program.Services.GetRequiredService<ILogger<MainWindow>>();

    /// <summary>
    ///     WPF's shutdown: stop what is running, then empty the temp folder beside the working directory, which
    ///     extraction can leave gigabytes in.
    /// </summary>
    private void OnClosed()
    {
        Logger.LogInformation("Beginning shutdown...");
        VM?.CancelRunningTasks(TimeSpan.FromSeconds(10));

        var tempDirectory = Path.Combine(Environment.CurrentDirectory, "temp");
        Logger.LogInformation("Clearing {TempDir}", tempDirectory);
        try
        {
            var directory = new DirectoryInfo(tempDirectory);
            foreach (var file in directory.EnumerateFiles()) file.Delete();
            foreach (var dir in directory.EnumerateDirectories()) dir.Delete(true);
            Logger.LogInformation("Finished clearing {TempDir}", tempDirectory);
        }
        catch (DirectoryNotFoundException)
        {
            Logger.LogInformation("Unable to find {TempDir}", tempDirectory);
        }
    }

    private readonly SerialDisposable _vmSubscriptions = new();
    private readonly SerialDisposable _progressSubscriptions = new();

    /// <summary>
    ///     What the WPF window bound by hand: the nav rail comes and goes with NavigationVisible, and a screen
    ///     that reports progress gets the two boxes in the title bar, fed from its ProgressViewModel.
    /// </summary>
    private void WatchViewModel()
    {
        var subscriptions = new CompositeDisposable();
        _vmSubscriptions.Disposable = subscriptions;
        if (VM is not { } vm) return;

        vm.WhenAnyValue(x => x.NavigationVisible)
            .Subscribe(visible =>
            {
                MainArea.ColumnDefinitions[0].Width = new GridLength(visible ? 115 : 0);
                NavRail.IsVisible = visible;
            })
            .DisposeWith(subscriptions);

        vm.WhenAnyValue(x => x.ActivePane)
            .Subscribe(pane =>
            {
                WizardSteps.IsVisible = pane is IProgressVM;
                _progressSubscriptions.Disposable = pane is ProgressViewModel progress ? WatchProgress(progress) : null;
            })
            .DisposeWith(subscriptions);
    }

    private IDisposable WatchProgress(ProgressViewModel wizard)
    {
        var subscriptions = new CompositeDisposable();

        wizard.WhenAnyValue(x => x.ConfigurationText)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(text => ConfigurationText.Text = text)
            .DisposeWith(subscriptions);
        wizard.WhenAnyValue(x => x.ProgressText)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(text => ProgressText.Text = text)
            .DisposeWith(subscriptions);
        wizard.WhenAnyValue(x => x.ProgressPercent.Value)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(p =>
            {
                // Shown only between the ends, and hidden rather than collapsed, as in WPF.
                ProgressPercentage.Opacity = p > 0 && p < 1 ? 1 : 0;
                ProgressPercentage.Text = (int)(p * 100) + "%";
                WizardProgress.Value = p;
            })
            .DisposeWith(subscriptions);
        wizard.WhenAnyValue(x => x.CurrentStep)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(step =>
            {
                ConfigurationText.Width = step == Step.Configuration ? 500 : double.NaN;
                ConfigurationText.HorizontalAlignment = step == Step.Configuration ? HorizontalAlignment.Left : HorizontalAlignment.Center;
                ProgressText.Width = step == Step.Busy ? 500 : double.NaN;
                ProgressText.HorizontalAlignment = step == Step.Busy ? HorizontalAlignment.Left : HorizontalAlignment.Center;
            })
            .DisposeWith(subscriptions);

        return subscriptions;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        HideSystemBorder();
    }

    /// <summary>
    ///     As WPF's title bar did: pressing it on a maximised window restores the window first, then the press
    ///     drags. There is no double-click to maximise; WPF had none.
    /// </summary>
    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
            WindowState = WindowState.Normal;

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    /// <summary>
    ///     The dimmed area around a floating pane drags the window like the title bar, and a click on it that
    ///     is over within a fifth of a second closes the pane. The drag holds the press until release, so
    ///     the time it took says which of the two happened.
    /// </summary>
    private void FloatingWindowBackground_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        var pressed = Stopwatch.StartNew();
        BeginMoveDrag(e);
        if (pressed.Elapsed < TimeSpan.FromSeconds(0.2))
            VM?.DismissFloatingPane();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && VM?.ActiveFloatingPane != null)
            VM.DismissFloatingPane();
    }

    private void Minimize_Click(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object? sender, RoutedEventArgs e) => ToggleMaximized();

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();

    private void ToggleMaximized()
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    /// <summary>
    /// Windows 11 draws a 1px border in the accent colour around every window. MahApps turned it off
    /// for the WPF app, which draws its own border in the background colour, so it is off here too.
    /// </summary>
    private void HideSystemBorder()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
            return;
        if (TryGetPlatformHandle()?.Handle is not { } hwnd || hwnd == IntPtr.Zero)
            return;

        var none = DwmColorNone;
        DwmSetWindowAttribute(hwnd, DwmwaBorderColor, ref none, sizeof(uint));
    }

    private const int DwmwaBorderColor = 34;
    private const uint DwmColorNone = 0xFFFFFFFE;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref uint value, int size);
}
