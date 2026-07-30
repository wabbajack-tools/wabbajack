using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace Wabbajack;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // TODO(avalonia-bootstrap): resolve MainWindowVM from the DI host (port App.xaml.cs
            // ConfigureServices) and assign it as DataContext. Placeholder shell for now.
            desktop.MainWindow = new Views.MainWindow();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
