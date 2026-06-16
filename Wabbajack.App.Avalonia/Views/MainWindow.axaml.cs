using System;
using Avalonia.Controls;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using Wabbajack.Messages;

namespace Wabbajack.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        Nav.DataContext = Program.Services.GetRequiredService<NavigationVM>();
        ActivePane.Content = Program.Services.GetRequiredService<HomeVM>();

        MessageBus.Current.Listen<NavigateToGlobal>()
            .Subscribe(m => Dispatcher.UIThread.Post(() => ActivePane.Content = Resolve(m.Screen)));

        MessageBus.Current.Listen<ShowFloatingWindow>()
            .Subscribe(m => Dispatcher.UIThread.Post(() => ShowFloating(m.Screen)));

        // Dismiss the floating pane on Escape or a click on the dimmed backdrop.
        KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Escape && FloatingLayer.IsVisible)
                ShowFloating(FloatingScreenType.None);
        };
        FloatingBackdrop.PointerPressed += (_, _) => ShowFloating(FloatingScreenType.None);
    }

    private void ShowFloating(FloatingScreenType screen)
    {
        object? content = screen switch
        {
            FloatingScreenType.ModListDetails => Program.Services.GetRequiredService<ModListDetailsVM>(),
            FloatingScreenType.FileUpload => Program.Services.GetRequiredService<FileUploadVM>(),
            _ => null
        };
        FloatingPane.Content = content;
        FloatingLayer.IsVisible = content != null;
    }

    private object Resolve(ScreenType screen) => screen switch
    {
        ScreenType.Home => Program.Services.GetRequiredService<HomeVM>(),
        ScreenType.ModListGallery => Program.Services.GetRequiredService<ModListGalleryVM>(),
        ScreenType.Settings => Program.Services.GetRequiredService<SettingsVM>(),
        ScreenType.Installer => Program.Services.GetRequiredService<InstallationVM>(),
        ScreenType.CompilerHome => Program.Services.GetRequiredService<CompilerHomeVM>(),
        ScreenType.CompilerMain => Program.Services.GetRequiredService<CompilerMainVM>(),
        ScreenType.Info => Program.Services.GetRequiredService<InfoVM>(),
        // Other screens resolve to a placeholder until their wave ports them.
        _ => new ScreenPlaceholderView(screen.ToString())
    };
}
