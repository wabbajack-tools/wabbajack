using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;

namespace Wabbajack;

public partial class ModListDetailsView : ReactiveUserControl<ModListDetailsVM>
{
    public ModListDetailsView()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
