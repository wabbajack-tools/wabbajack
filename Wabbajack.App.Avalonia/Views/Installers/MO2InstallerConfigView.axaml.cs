using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;

namespace Wabbajack;

public partial class MO2InstallerConfigView : ReactiveUserControl<MO2InstallerVM>
{
    public MO2InstallerConfigView()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
