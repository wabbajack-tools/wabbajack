using Avalonia.ReactiveUI;
using ReactiveUI;
using Wabbajack.App.Avalonia.ViewModels.Compiler;

namespace Wabbajack.App.Avalonia.Views.Compiler;

public partial class CompilerFileManagerView : ReactiveUserControl<CompilerFileManagerVM>
{
    public CompilerFileManagerView()
    {
        InitializeComponent();
        // Activating the view activates the view model, which is when it reads the list's folder into the tree.
        this.WhenActivated(_ => { });
    }
}
