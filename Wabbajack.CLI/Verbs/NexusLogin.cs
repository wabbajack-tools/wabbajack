using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fizzler.Systems.HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Wabbajack.CLI.Browser;
using Wabbajack.DTOs.Logins;
using Wabbajack.Services.OSIntegrated;

namespace Wabbajack.CLI.Verbs;

public class NexusLogin : AVerb
{
    private readonly ILogger<NexusLogin> _logger;
    private readonly BrowserHost _host;
    private readonly EncryptedJsonTokenProvider<NexusApiState> _tokenProvider;

    public NexusLogin(ILogger<NexusLogin> logger, BrowserHost host, EncryptedJsonTokenProvider<NexusApiState> tokenProvider)
    {
        _logger = logger;
        _host = host;
        _tokenProvider = tokenProvider;
    }
    
    public static Command MakeCommand()
    {
        var command = new Command("nexus-login");
        command.Description = "Prompt the user to log into the nexus";
        return command;
    }
    
    public async Task<int> Run(CancellationToken token)
    {
        var browser = await _host.CreateBrowser();
        
                token.ThrowIfCancellationRequested();

        browser.Instructions = "Please log into the Nexus";

        await browser.WaitForReady();

        await browser.NavigateTo(new Uri(
            "https://users.nexusmods.com/auth/continue?client_id=nexus&redirect_uri=https://www.nexusmods.com/oauth/callback&response_type=code&referrer=//www.nexusmods.com"));


        Cookie[] cookies = { };
        while (true)
        {
            cookies = await browser.Cookies("nexusmods.com", token);
            if (cookies.Any(c => c.Name == "member_id"))
                break;

            token.ThrowIfCancellationRequested();
            await Task.Delay(500, token);
        }

        browser.Instructions = "Getting API Key...";

        await browser.NavigateTo(new Uri("https://www.nexusmods.com/users/myaccount?tab=api"));

        var key = "";

        while (true)
        {
            try
            {
                key = (await browser.GetDom(token))
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
                await browser.EvaluateJavaScript(
                    "var found = document.querySelector(\"input[value=wabbajack]\").parentElement.parentElement.querySelector(\"form button[type=submit]\");" +
                    "found.onclick= function() {return true;};" +
                    "found.class = \" \"; " +
                    "found.click();" +
                    "found.remove(); found = undefined;"
                );
                browser.Instructions = "Generating API Key, Please Wait...";
            }
            catch (Exception)
            {
                // ignored
            }

            token.ThrowIfCancellationRequested();
            await Task.Delay(500, token);
        }

        browser.Instructions = "Success, saving information...";
        await _tokenProvider.SetToken(new NexusApiState
        {
            Cookies = cookies,
            ApiKey = key
        });
        
        return 0;
    }

    protected override ICommandHandler GetHandler()
    {
        return CommandHandler.Create(Run);
    }
}