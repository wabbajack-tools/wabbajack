using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;

namespace Wabbajack;

public partial class ContributorView : ReactiveUserControl<ContributorVM>
{
    public ContributorView()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
