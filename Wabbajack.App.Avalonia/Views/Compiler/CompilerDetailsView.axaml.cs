using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;

namespace Wabbajack;

public partial class CompilerDetailsView : ReactiveUserControl<CompilerDetailsVM>
{
    public CompilerDetailsView()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
