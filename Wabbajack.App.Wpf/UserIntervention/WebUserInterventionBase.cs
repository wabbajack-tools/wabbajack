using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.DTOs.Interventions;
using Wabbajack.Interventions;
using Wabbajack.Models;
using Wabbajack.WebAutomation;

namespace Wabbajack.UserIntervention;

public abstract class WebUserInterventionBase<T>
where T : IUserIntervention
{
    protected readonly WebBrowserVM Browser;
    protected readonly ILogger Logger;
    protected T Message;
    protected ViewModel PrevPane;
    protected IWebDriver Driver;

    protected WebUserInterventionBase(ILogger logger, WebBrowserVM browser, CefService service)
    {
        Logger = logger;
        Browser = browser;
        Driver = new CefSharpWrapper(logger, browser.Browser, service);
    }
    
    public void Configure(ViewModel prevPane, T message)
    {
        Message = message;
        PrevPane = prevPane;
    }

    protected void UpdateStatus(string status)
    {
        Browser.Instructions = status;
    }

    protected async Task NavigateTo(Uri uri)
    {
        await Driver.NavigateTo(uri, Message.Token);
    }

    public abstract Task Begin();

}