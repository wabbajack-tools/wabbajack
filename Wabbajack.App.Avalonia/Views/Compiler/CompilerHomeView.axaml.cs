using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;

namespace Wabbajack;

public partial class CompilerHomeView : ReactiveUserControl<CompilerHomeVM>
{
    public CompilerHomeView()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
