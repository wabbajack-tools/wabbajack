using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;

namespace Wabbajack;

public partial class PerformanceSettingsView : ReactiveUserControl<PerformanceSettingsVM>
{
    public PerformanceSettingsView()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
