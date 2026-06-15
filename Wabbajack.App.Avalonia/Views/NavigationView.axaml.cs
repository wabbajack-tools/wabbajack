using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;

namespace Wabbajack;

public partial class NavigationView : ReactiveUserControl<NavigationVM>
{
    public NavigationView()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
