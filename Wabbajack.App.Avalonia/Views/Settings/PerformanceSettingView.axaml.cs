using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;

namespace Wabbajack;

public partial class PerformanceSettingView : ReactiveUserControl<PerformanceSettingVM>
{
    public PerformanceSettingView()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
