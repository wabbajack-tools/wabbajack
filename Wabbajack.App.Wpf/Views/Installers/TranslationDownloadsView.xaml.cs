using System.Reactive.Disposables;
using ReactiveUI;

namespace Wabbajack;

public partial class TranslationDownloadsView : ReactiveUserControl<TranslationDownloadsVM>
{
    public TranslationDownloadsView()
    {
        InitializeComponent();
        this.WhenActivated(disposables =>
        {
            this.OneWayBind(ViewModel, vm => vm.Title, view => view.TitleText.Text)
                .DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.Explanation, view => view.ExplanationText.Text)
                .DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.Downloads, view => view.DownloadsView.ViewModel)
                .DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.CloseCommand, view => view.ContinueButton)
                .DisposeWith(disposables);
        });
    }
}
