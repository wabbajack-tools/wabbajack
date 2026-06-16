using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;

namespace Wabbajack;

public partial class ModListTileView : ReactiveUserControl<BaseModListMetadataVM>
{
    public ModListTileView()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
