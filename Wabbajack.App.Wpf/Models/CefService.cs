using System;
using System.Reactive.Subjects;
using Microsoft.Extensions.Logging;

namespace Wabbajack.Models;

public class CefService
{
    private readonly ILogger<CefService> _logger;
    private bool Inited { get;  set; } = false;

    private readonly Subject<string> _schemeStream = new();
    public IObservable<string> SchemeStream => _schemeStream;

    public CefService(ILogger<CefService> logger)
    {
        _logger = logger;
        Inited = false;
        Init();
    }

    public dynamic CreateBrowser()
    {
        return 0;
    }
    private void Init()
    {

    }

}