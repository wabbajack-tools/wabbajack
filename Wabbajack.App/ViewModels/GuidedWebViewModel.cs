using System.Reactive.Disposables;
using System.Threading;
using System.Threading.Tasks;
using CefNet;
using CefNet.Avalonia;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.App.Messages;
using Wabbajack.Common;

namespace Wabbajack.App.ViewModels
{
    public abstract class GuidedWebViewModel : ViewModelBase, IReceiverMarker
    {
        protected ILogger _logger;

        [Reactive]
        public string Instructions { get; set; }
        
        public GuidedWebViewModel(ILogger logger)
        {
            _logger = logger;
            Activator = new ViewModelActivator();
            
            this.WhenActivated(disposables =>
            {
                Disposable.Empty.DisposeWith(disposables);
            });
        }

        public WebView Browser { get; set; }

        public abstract Task Run(CancellationToken token);
    }
}