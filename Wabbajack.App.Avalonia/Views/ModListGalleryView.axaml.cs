using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;

namespace Wabbajack;

public partial class ModListGalleryView : ReactiveUserControl<ModListGalleryVM>
{
    public ModListGalleryView()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
