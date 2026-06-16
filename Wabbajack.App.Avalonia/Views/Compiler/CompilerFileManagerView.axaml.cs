using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;

namespace Wabbajack;

public partial class CompilerFileManagerView : ReactiveUserControl<CompilerFileManagerVM>
{
    public CompilerFileManagerView()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
