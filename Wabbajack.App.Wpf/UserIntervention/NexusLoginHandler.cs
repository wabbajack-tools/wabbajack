using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fizzler.Systems.HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Wabbajack.DTOs.Logins;
using Wabbajack.Messages;
using Wabbajack.Models;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.Services.OSIntegrated;

namespace Wabbajack.UserIntervention;

public class NexusLoginHandler : BrowserWindowViewModel
{
    private readonly EncryptedJsonTokenProvider<NexusApiState> _tokenProvider;

    public NexusLoginHandler(EncryptedJsonTokenProvider<NexusApiState> tokenProvider)
    {
        HeaderText = "Nexus Login";
        _tokenProvider = tokenProvider;
    }

    protected override async Task Run(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        Instructions = "Please log into the Nexus";

        await NavigateTo(new Uri(
            "https://users.nexusmods.com/auth/continue?client_id=nexus&redirect_uri=https://www.nexusmods.com/oauth/callback&response_type=code&referrer=//www.nexusmods.com"));


        Cookie[] cookies = { };
        while (true)
        {
            cookies = await GetCookies("nexusmods.com", token);
            if (cookies.Any(c => c.Name == "member_id"))
                break;

            token.ThrowIfCancellationRequested();
            await Task.Delay(500, token);
        }

        Instructions = "Getting API Key...";

        await NavigateTo(new Uri("https://next.nexusmods.com/settings/api-keys"));

        var key = "";

        while (true)
        {
            try
            {
                key = (await GetDom(token)).DocumentNode.QuerySelectorAll("img[alt='Wabbajack']").SelectMany(p => p.ParentNode.ParentNode.QuerySelectorAll("input[aria-label='api key']")).Select(node => node.Attributes["value"]).FirstOrDefault()?.Value;
                //var apiKey = (await GetDom(token)).DocumentNode.QuerySelectorAll("input[aria-label='api key']");
                /*
                key = (await GetDom(token))
                    .DocumentNode
                    .QuerySelectorAll("input[aria-label='api key']")
                    .SelectMany(p => p.ParentNode.ParentNode.QuerySelectorAll("textarea.application-key"))
                    .Select(node => node.InnerHtml)
                    .FirstOrDefault() ?? "";
                */
            }
            catch (Exception)
            {
                // ignored
            }

            if (!string.IsNullOrEmpty(key))
                break;

            try
            {
                await EvaluateJavaScript(
                    "var found = document.querySelector(\"input[value=wabbajack]\").parentElement.parentElement.querySelector(\"form button[type=submit]\");" +
                    "found.onclick= function() {return true;};" +
                    "found.class = \" \"; " +
                    "found.click();" +
                    "found.remove(); found = undefined;"
                );
                Instructions = "Generating API Key, Please Wait...";
            }
            catch (Exception)
            {
                // ignored
            }

            token.ThrowIfCancellationRequested();
            await Task.Delay(500, token);
        }

        Instructions = "Success, saving information...";
        await _tokenProvider.SetToken(new NexusApiState
        {
            Cookies = cookies,
            ApiKey = key
        });
    }
}