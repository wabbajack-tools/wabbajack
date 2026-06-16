using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;

namespace Wabbajack;

public partial class CompiledModListTileView : ReactiveUserControl<CompiledModListTileVM>
{
    public CompiledModListTileView()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
