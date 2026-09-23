using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
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
    }

    private MainWindowVM? VM => DataContext as MainWindowVM;

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
