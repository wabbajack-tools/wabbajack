using System.Reactive.Disposables;
using ReactiveUI;
using Wabbajack.App.Views;

namespace Wabbajack.App.Screens;

public partial class ErrorPageView : ScreenBase<ErrorPageViewModel>
{
    public ErrorPageView() : base("Error")
    {
        InitializeComponent();
        this.WhenActivated(disposables =>
        {
            this.Bind(ViewModel, vm => vm.Prefix, view => view.Prefix.Text)
                .DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.ShortMessage, view => view.Message.Text)
                .DisposeWith(disposables);
        });
    }
}