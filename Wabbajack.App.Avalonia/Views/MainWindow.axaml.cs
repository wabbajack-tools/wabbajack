using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.ReactiveUI;
using Wabbajack.App.Avalonia.Messages;
using Wabbajack.App.Avalonia.ViewModels;

namespace Wabbajack.App.Avalonia.Views;

public partial class MainWindow : ReactiveWindow<MainWindowVM>
{
    public MainWindow()
    {
        InitializeComponent();

        TitleBarGrid.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                BeginMoveDrag(e);
        };

        MinimizeButton.Click += (_, _) => WindowState = WindowState.Minimized;

        MaximizeButton.Click += (_, _) =>
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;

        CloseButton.Click += (_, _) => Close();

        LoadLocalFileButton.Click += (_, _) =>
            NavigateToGlobal.Send(ScreenType.ModListGallery);

        GetHelpButton.Click += (_, _) =>
            Process.Start(new ProcessStartInfo("https://wiki.wabbajack.org/") { UseShellExecute = true });
    }
}
