using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;

namespace Wabbajack;

public partial class MiscSettingsView : ReactiveUserControl<SettingsVM>
{
    public MiscSettingsView()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
