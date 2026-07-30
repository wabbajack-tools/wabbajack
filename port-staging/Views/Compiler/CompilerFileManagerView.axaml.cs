using System.Reactive.Disposables;
using ReactiveUI;
using Avalonia.ReactiveUI;

namespace Wabbajack;

/// <summary>
/// Interaction logic for CompilerFileManagerView.axaml
/// </summary>
public partial class CompilerFileManagerView : ReactiveUserControl<CompilerFileManagerVM>
{
    public CompilerFileManagerView()
    {
        InitializeComponent();


        this.WhenActivated(disposables =>
        {
            this.WhenAny(x => x.ViewModel.Files)
                .BindToStrict(this, v => v.FileTreeView.ItemsSource)
                .DisposeWith(disposables);
        });

    }

}
