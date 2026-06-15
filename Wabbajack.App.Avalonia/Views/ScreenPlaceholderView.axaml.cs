using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Wabbajack.Views;

public partial class ScreenPlaceholderView : UserControl
{
    public ScreenPlaceholderView()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public ScreenPlaceholderView(string screenName) : this()
    {
        Label.Text = $"{screenName} — not yet ported";
    }
}
