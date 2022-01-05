using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using Wabbajack.DTOs.Logins;
using Wabbajack.LibCefHelpers;
using Wabbajack.Messages;
using Wabbajack.Models;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.Services.OSIntegrated.TokenProviders;

namespace Wabbajack.UserIntervention;

public class NexusLoginHandler : WebUserInterventionBase<NexusLogin>
{
    private readonly ITokenProvider<NexusApiState> _provider;

    public NexusLoginHandler(ILogger<NexusLoginHandler> logger, WebBrowserVM browserVM, ITokenProvider<NexusApiState> provider, CefService service) 
        : base(logger, browserVM, service)
    {
        _provider = provider;
    }
    public override async Task Begin()
    {
        try
        {
            Messages.NavigateTo.Send(Browser);
            UpdateStatus("Please log into the Nexus");
            await Driver.WaitForInitialized();
            
            await NavigateTo(new Uri("https://users.nexusmods.com/auth/continue?client_id=nexus&redirect_uri=https://www.nexusmods.com/oauth/callback&response_type=code&referrer=//www.nexusmods.com"));

            Cookie[] cookies = {};
            while (true)
            {
                cookies = await Driver.GetCookies("nexusmods.com");
                if (cookies.Any(c => c.Name == "member_id"))
                    break;
                Message.Token.ThrowIfCancellationRequested();
                await Task.Delay(500, Message.Token);
            }


            await NavigateTo(new Uri("https://www.nexusmods.com/users/myaccount?tab=api"));

            UpdateStatus("Looking for API Key");

            var key = "";

            while (true)
            {
                try
                {
                    key = await Driver.EvaluateJavaScript(
                        "document.querySelector(\"input[value=wabbajack]\").parentElement.parentElement.querySelector(\"textarea.application-key\").innerHTML");
                }
                catch (Exception)
                {
                    // ignored
                }

                if (!string.IsNullOrEmpty(key))
                {
                    break;
                }

                try
                {
                    await Driver.EvaluateJavaScript(
                        "var found = document.querySelector(\"input[value=wabbajack]\").parentElement.parentElement.querySelector(\"form button[type=submit]\");" +
                        "found.onclick= function() {return true;};" +
                        "found.class = \" \"; " +
                        "found.click();" +
                        "found.remove(); found = undefined;"
                    );
                    UpdateStatus("Generating API Key, Please Wait...");


                }
                catch (Exception)
                {
                    // ignored
                }

                Message.Token.ThrowIfCancellationRequested();
                await Task.Delay(500, Message.Token);
            }


            await _provider.SetToken(new NexusApiState()
            {
                ApiKey = key,
                Cookies = cookies
            });

            ((NexusLogin)Message).CompletionSource.SetResult();
            Messages.NavigateTo.Send(PrevPane);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "While logging into Nexus Mods");
            Message.SetException(ex);
            Messages.NavigateTo.Send(PrevPane);
        }
    }
}