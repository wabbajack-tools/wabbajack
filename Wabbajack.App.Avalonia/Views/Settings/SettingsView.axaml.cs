using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;

namespace Wabbajack;

public partial class SettingsView : ReactiveUserControl<SettingsVM>
{
    public SettingsView()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
