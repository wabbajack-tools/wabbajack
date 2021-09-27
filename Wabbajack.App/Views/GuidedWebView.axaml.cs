using System.Reactive.Disposables;
using System.Text;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CefNet;
using ReactiveUI;
using Wabbajack.App.ViewModels;
using Wabbajack.Common;

namespace Wabbajack.App.Views
{
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
}