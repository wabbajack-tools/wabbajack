using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;

namespace Wabbajack;

public partial class LoginItemView : ReactiveUserControl<LoginTargetVM>
{
    public LoginItemView()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
