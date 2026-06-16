using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;

namespace Wabbajack;

public partial class InstallationView : ReactiveUserControl<InstallationVM>
{
    public InstallationView()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
