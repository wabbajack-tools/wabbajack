using System.Reactive.Disposables;
using System.Threading;
using System.Threading.Tasks;
using CefNet.Avalonia;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.App.Messages;

namespace Wabbajack.App.ViewModels;

public abstract class GuidedWebViewModel : ViewModelBase
{
    protected ILogger _logger;

    public GuidedWebViewModel(ILogger logger)
    {
        _logger = logger;
        Activator = new ViewModelActivator();

        this.WhenActivated(disposables => { Disposable.Empty.DisposeWith(disposables); });
    }

    [Reactive] public string Instructions { get; set; }

    public WebView Browser { get; set; }

    public abstract Task Run(CancellationToken token);
}