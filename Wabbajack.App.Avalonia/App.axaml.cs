using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;

namespace Wabbajack;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = Program.Services.GetRequiredService<MainWindow>();
            window.DataContext = Program.Services.GetRequiredService<MainWindowVM>();
            desktop.MainWindow = window;
        }
        base.OnFrameworkInitializationCompleted();
    }
}
