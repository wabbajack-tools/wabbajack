using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fizzler.Systems.HtmlAgilityPack;
using ReactiveUI;
using Wabbajack.CLI.Verbs;
using Wabbajack.DTOs.Logins;
using Wabbajack.Networking.Http.Interfaces;

namespace Wabbajack.Networking.Browser.Verbs;

public class NexusLogin : AVerb
{
    private readonly ITokenProvider<NexusApiState> _tokenProvider;

    public NexusLogin(ITokenProvider<NexusApiState> tokenProvider)
    {
        _tokenProvider = tokenProvider;
    }
    public override Command MakeCommand()
    {
        var command = new Command("nexus-login");
        command.Description = "Prompt the user to log into the nexus";
        command.Handler = CommandHandler.Create(Run);
        return command;
    }

    private async Task Run(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        Instructions = "Please log into the Nexus";

        await Browser.WaitForReady();

        await Browser.NavigateTo(new Uri(
            "https://users.nexusmods.com/auth/continue?client_id=nexus&redirect_uri=https://www.nexusmods.com/oauth/callback&response_type=code&referrer=//www.nexusmods.com"));
        
        Cookie[] cookies = { };
        while (true)
        {
            cookies = await Browser.Cookies("nexusmods.com", token);
            if (cookies.Any(c => c.Name == "member_id"))
                break;

            token.ThrowIfCancellationRequested();
            await Task.Delay(500, token);
        }
        
        Instructions = "Getting API Key...";
        
        await Browser.NavigateTo(new Uri("https://www.nexusmods.com/users/myaccount?tab=api"));
        
        
        await Browser.NavigateTo(new Uri("https://www.nexusmods.com/users/myaccount?tab=api"));

        var key = "";

        while (true)
        {
            try
            {
                key = (await Browser.GetDom(token))
                    .DocumentNode
                    .QuerySelectorAll("input[value=wabbajack]")
                    .SelectMany(p => p.ParentNode.ParentNode.QuerySelectorAll("textarea.application-key"))
                    .Select(node => node.InnerHtml)
                    .FirstOrDefault() ?? "";
            }
            catch (Exception)
            {
                // ignored
            }

            if (!string.IsNullOrEmpty(key))
                break;

            try
            {
                await Browser.EvaluateJavaScript(
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
        }

        Instructions = "Success, saving information...";
        await _tokenProvider.SetToken(new NexusApiState
        {
            Cookies = cookies,
            ApiKey = key
        });

    }
}