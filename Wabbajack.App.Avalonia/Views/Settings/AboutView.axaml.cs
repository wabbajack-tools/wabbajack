using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;

namespace Wabbajack;

public partial class AboutView : ReactiveUserControl<AboutVM>
{
    public AboutView()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
