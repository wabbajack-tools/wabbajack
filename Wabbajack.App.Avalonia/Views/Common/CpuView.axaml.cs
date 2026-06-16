using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;

namespace Wabbajack;

public partial class CpuView : ReactiveUserControl<ICpuStatusVM>
{
    public CpuView()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
