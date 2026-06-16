using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;

namespace Wabbajack;

public partial class LoginSettingsView : ReactiveUserControl<LoginManagerVM>
{
    public LoginSettingsView()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
