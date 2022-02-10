using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using Wabbajack.Common;
using Wabbajack.Networking.BethesdaNet;
using Wabbajack.Services.OSIntegrated;

namespace Wabbajack.App.Blazor.Browser.ViewModels;

public class BethesdaNetLogin : BrowserTabViewModel
{
    private readonly EncryptedJsonTokenProvider<Wabbajack.DTOs.Logins.BethesdaNetLoginState> _tokenProvider;
    private readonly Client _client;

    public BethesdaNetLogin(EncryptedJsonTokenProvider<Wabbajack.DTOs.Logins.BethesdaNetLoginState> tokenProvider, Wabbajack.Networking.BethesdaNet.Client client)
    {
        _tokenProvider = tokenProvider;
        _client = client;
        HeaderText = "Bethesda Net Login";
    }
    protected override async Task Run(CancellationToken token)
    {
        await WaitForReady();
        Instructions = "Please log in to Bethesda.net";

        string requestJson = "";
        
        Browser.Browser.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
        Browser.Browser.CoreWebView2.WebResourceRequested += (sender, args) =>
        {
            if (args.Request.Uri == "https://api.bethesda.net/dwemer/attunement/v1/authenticate" && args.Request.Method == "POST")
            {
                requestJson = args.Request.Content.ReadAllText();
                args.Request.Content = new MemoryStream(Encoding.UTF8.GetBytes(requestJson));
            }
        };
        
        await NavigateTo(new Uri("https://bethesda.net/en/dashboard"));

        while (true)
        {
            var code = await GetCookies("bethesda.net", token);
            if (code.Any(c => c.Name == "bnet-session")) break;

        }

        var data = JsonSerializer.Deserialize<LoginRequest>(requestJson);

        var provider = new Wabbajack.DTOs.Logins.BethesdaNetLoginState()
        {
            Username = data.UserName,
            Password = data.Password
        };
        await _tokenProvider.SetToken(provider);
        await _client.Login(token);
        
        await Task.Delay(10);

    }

    public class LoginRequest
    {
        [JsonPropertyName("username")]
        public string UserName { get; set; }
        [JsonPropertyName("password")]
        public string Password { get; set; }
    }
    
}
