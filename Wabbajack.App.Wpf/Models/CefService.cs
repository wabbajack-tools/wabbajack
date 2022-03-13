using System;
using System.Reactive.Subjects;
using CefSharp;
using CefSharp.Wpf;
using Microsoft.Extensions.Logging;

namespace Wabbajack.Models;

public class CefService
{
    private readonly ILogger<CefService> _logger;
    private bool Inited { get;  set; } = false;

    private readonly Subject<string> _schemeStream = new();
    public IObservable<string> SchemeStream => _schemeStream;
    public Func<IBrowser, IFrame, string, IRequest, IResourceHandler>? SchemeHandler { get; set; }

    public CefService(ILogger<CefService> logger)
    {
        _logger = logger;
        Inited = false;
        Init();
    }

    public IWebBrowser CreateBrowser()
    {
        return new ChromiumWebBrowser();
    }
    private void Init()
    {
        if (Inited || Cef.IsInitialized) return;
        Inited = true;
        var settings = new CefSettings
        {
            CachePath = Consts.CefCacheLocation.ToString(),
            UserAgent = "Wabbajack In-App Browser"
        };
        settings.RegisterScheme(new CefCustomScheme()
        {
            SchemeName = "wabbajack", 
            SchemeHandlerFactory = new SchemeHandlerFactor(_logger, this)
        });
        
        _logger.LogInformation("Initializing Cef");
        if (!Cef.Initialize(settings))
        {
            _logger.LogError("Cannot initialize CEF");
        }
    }

    private class SchemeHandlerFactor : ISchemeHandlerFactory
    {
        private readonly ILogger _logger;
        private readonly CefService _service;

        internal SchemeHandlerFactor(ILogger logger, CefService service)
        {
            _logger = logger;
            _service = service;
        }

        public IResourceHandler Create(IBrowser browser, IFrame frame, string schemeName, IRequest request)
        {
            _logger.LogInformation("Scheme handler Got: {Scheme} : {Url}", schemeName, request.Url);
            if (schemeName == "wabbajack")
            {
                _service._schemeStream.OnNext(request.Url);
            }
            return new ResourceHandler();
        }
    }
}