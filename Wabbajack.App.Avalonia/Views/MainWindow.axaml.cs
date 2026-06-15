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

        ActivePane.Content = Program.Services.GetRequiredService<HomeVM>();

        MessageBus.Current.Listen<NavigateToGlobal>()
            .Subscribe(m => Dispatcher.UIThread.Post(() => ActivePane.Content = Resolve(m.Screen)));
    }

    private object Resolve(ScreenType screen) => screen switch
    {
        ScreenType.Home => Program.Services.GetRequiredService<HomeVM>(),
        // Other screens are added as their VMs are ported in later waves.
        _ => ActivePane.Content!
    };
}
