using System.Reactive.Disposables;
using System.Threading;
using ReactiveUI;
using Wabbajack.App.ViewModels;
using Wabbajack.Common;

namespace Wabbajack.App.Views;

public partial class GuidedWebView : ScreenBase<GuidedWebViewModel>
{
    public GuidedWebView() : base(false)
    {
        InitializeComponent();

        this.WhenActivated(disposables =>
        {
            ViewModel.Browser = WebView;
            this.Bind(ViewModel, vm => vm.Instructions, view => view.Instructions.Text)
                .DisposeWith(disposables);
            ViewModel.Run(CancellationToken.None).FireAndForget();
        });
    }
}