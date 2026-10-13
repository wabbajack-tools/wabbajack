using Avalonia.ReactiveUI;
using ReactiveUI;
using Wabbajack.App.Avalonia.ViewModels.Compiler;

namespace Wabbajack.App.Avalonia.Views.Compiler;

public partial class CompilerHomeView : ReactiveUserControl<CompilerHomeVM>
{
    public CompilerHomeView()
    {
        InitializeComponent();
        this.WhenActivated(_ => { });
    }
}
