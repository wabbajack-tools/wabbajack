using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;

namespace Wabbajack;

public partial class InfoView : ReactiveUserControl<InfoVM>
{
    public InfoView()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
