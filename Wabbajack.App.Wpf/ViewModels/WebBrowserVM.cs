using System;
using System.Reactive;
using System.Reactive.Subjects;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.Messages;
using Wabbajack.Models;

namespace Wabbajack;

public class WebBrowserVM : ViewModel, IBackNavigatingVM, IDisposable
{
    private readonly ILogger<WebBrowserVM> _logger;
    private readonly CefService _cefService;

    [Reactive]
    public string Instructions { get; set; }

    public dynamic Browser { get; }
    public dynamic Driver { get; set; }

    [Reactive]
    public ViewModel NavigateBackTarget { get; set; }

    [Reactive] public ICommand CloseCommand { get; set; }

    public Subject<bool> IsBackEnabledSubject { get; } = new Subject<bool>();
    public IObservable<bool> IsBackEnabled { get; }

    public WebBrowserVM(ILogger<WebBrowserVM> logger, CefService cefService)
    {
        // CefService is required so that Cef is initalized
        _logger = logger;
        _cefService = cefService;
        Instructions = "Wabbajack Web Browser";
        
        CloseCommand = ReactiveCommand.Create(NavigateBack.Send);
        //Browser = cefService.CreateBrowser();
        //Driver = new CefSharpWrapper(_logger, Browser, cefService);

    }

    public override void Dispose()
    {
        Browser.Dispose();
        base.Dispose();
    }
}
