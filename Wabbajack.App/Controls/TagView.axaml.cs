using System.Reactive.Disposables;
using Avalonia.Controls;
using Avalonia.ReactiveUI;
using ReactiveUI;

namespace Wabbajack.App.Controls;

public partial class TagView : ReactiveUserControl<TagViewModel>
{
    public TagView()
    {
        InitializeComponent();
        this.WhenActivated(disposables =>
        {
            this.OneWayBind(ViewModel, vm => vm.Name, view => view.Text.Text)
                .DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.Tag, view => view.Classes,
                    c => c == null ? new Classes() : new Classes(c))
                .DisposeWith(disposables);
        });
    }
}