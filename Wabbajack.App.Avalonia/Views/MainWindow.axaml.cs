using System.Diagnostics;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.ReactiveUI;
using Wabbajack.App.Avalonia.Messages;
using Wabbajack.App.Avalonia.ViewModels;
using Wabbajack.DTOs;
using Wabbajack.Paths;

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

        LoadLocalFileButton.Click += async (_, _) =>
        {
            var result = await TopLevel.GetTopLevel(this)!.StorageProvider
                .OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Select Wabbajack archive",
                    AllowMultiple = false,
                    FileTypeFilter = new[]
                    {
                        new FilePickerFileType("Wabbajack archive") { Patterns = new[] { "*.wabbajack" } },
                        FilePickerFileTypes.All
                    }
                });

            if (result.Count > 0)
            {
                var localPath = result[0].Path.LocalPath;
                var name = Path.GetFileNameWithoutExtension(localPath);
                LoadModlistForInstalling.Send(
                    (AbsolutePath)localPath,
                    new ModlistMetadata { Title = name });
                NavigateToGlobal.Send(ScreenType.Installer);
            }
        };

        GetHelpButton.Click += (_, _) =>
            Process.Start(new ProcessStartInfo("https://wiki.wabbajack.org/") { UseShellExecute = true });
    }
}
