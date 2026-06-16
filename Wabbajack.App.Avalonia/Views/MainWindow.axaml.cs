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
    }

    private object Resolve(ScreenType screen) => screen switch
    {
        ScreenType.Home => Program.Services.GetRequiredService<HomeVM>(),
        ScreenType.ModListGallery => Program.Services.GetRequiredService<ModListGalleryVM>(),
        ScreenType.Settings => Program.Services.GetRequiredService<SettingsVM>(),
        ScreenType.Installer => Program.Services.GetRequiredService<InstallationVM>(),
        ScreenType.CompilerHome => Program.Services.GetRequiredService<CompilerHomeVM>(),
        ScreenType.CompilerMain => Program.Services.GetRequiredService<CompilerMainVM>(),
        // Other screens resolve to a placeholder until their wave ports them.
        _ => new ScreenPlaceholderView(screen.ToString())
    };
}
