using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Wabbajack.App.Avalonia.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        TitleBar.PointerPressed += TitleBar_PointerPressed;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        HideSystemBorder();
    }

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        if (e.ClickCount == 2)
            ToggleMaximized();
        else
            BeginMoveDrag(e);
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
