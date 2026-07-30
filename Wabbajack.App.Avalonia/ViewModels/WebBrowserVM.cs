using System;
using System.Reactive;
using System.Reactive.Subjects;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Wabbajack.Messages;
using Wabbajack.Models;

namespace Wabbajack;

public partial class WebBrowserVM : ViewModel, IBackNavigatingVM, IDisposable
{
    private readonly ILogger<WebBrowserVM> _logger;

    [Reactive]
    public partial string Instructions { get; set; }

    public dynamic Browser { get; }
    public dynamic Driver { get; set; }

    [Reactive]
    public partial ViewModel NavigateBackTarget { get; set; }

    [Reactive] public partial ICommand CloseCommand { get; set; }

    public Subject<bool> IsBackEnabledSubject { get; } = new Subject<bool>();
    public IObservable<bool> IsBackEnabled { get; }

    // The CefSharp-era browser was already dead code before the Avalonia port (CefService.CreateBrowser
    // returned 0 and every call site was commented out); the live browser is BrowserWindowViewModel,
    // hosted by Views/BrowserWindow over WebView2. This view model is kept only because MainWindowVM
    // and the MainWindow DataTemplate map still reference it.
    public WebBrowserVM(ILogger<WebBrowserVM> logger)
    {
        _logger = logger;
        Instructions = "Wabbajack Web Browser";

        CloseCommand = ReactiveCommand.Create(NavigateBack.Send);
    }

    public override void Dispose()
    {
        (Browser as IDisposable)?.Dispose();
        base.Dispose();
    }
}
