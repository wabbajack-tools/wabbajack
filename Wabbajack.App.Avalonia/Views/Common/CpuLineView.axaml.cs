using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;

namespace Wabbajack;

public partial class CpuLineView : ReactiveUserControl<CPUDisplayVM>
{
    public CpuLineView()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
