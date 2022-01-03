using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.DTOs.Interventions;
using Wabbajack.Interventions;
using Wabbajack.WebAutomation;

namespace Wabbajack.UserIntervention;

public class WebUserInterventionBase
{
    protected readonly WebBrowserVM Browser;
    protected readonly ILogger Logger;
    protected IUserIntervention Message;
    protected ViewModel PrevPane;
    protected IWebDriver Driver;

    public WebUserInterventionBase(ILogger logger, WebBrowserVM browser)
    {
        Logger = logger;
        Browser = browser;
        Driver = new CefSharpWrapper(logger, browser.Browser);
    }
    
    public void Configure(ViewModel prevPane, IUserIntervention message)
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

}