using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using CefNet;
using Microsoft.Extensions.Logging;
using Wabbajack.CLI.Verbs;
using Wabbajack.DTOs.Logins;
using Wabbajack.Services.OSIntegrated;

namespace Wabbajack.Networking.Browser.Verbs;

public abstract class AOAuthLoginVerb<TLoginType> : AVerb
where TLoginType : OAuth2LoginState, new()
{
    private readonly EncryptedJsonTokenProvider<TLoginType> _tokenProvider;
    private readonly HttpClient _httpClient;
    private readonly string _namePrefix;
    private readonly ILogger _logger;

    public AOAuthLoginVerb(ILogger logger, string namePrefix, EncryptedJsonTokenProvider<TLoginType> tokenProvider, HttpClient client)
    {
        _logger = logger;
        _tokenProvider = tokenProvider;
        _httpClient = client;
        _namePrefix = namePrefix;


    }

    public override Command MakeCommand()
    {
        var textInfo = new CultureInfo("en-US", false).TextInfo;
        var command = new Command($"{_namePrefix}-login");
        command.Description = $"Prompt the user to log into {textInfo.ToTitleCase(_namePrefix.Replace("-", " "))}";
        command.Handler = CommandHandler.Create(Run);
        return command;
    }

    private async Task Run(CancellationToken token)
    {
        var tlogin = new TLoginType();

        await Browser.WaitForReady();

        var handler = new AsyncSchemeHandler();
        Browser.RequestContext.RegisterSchemeHandlerFactory("wabbajack", "", handler);

        Instructions = $"Please log in and allow Wabbajack to access your {tlogin.SiteName} account";

        var scopes = string.Join(" ", tlogin.Scopes);
        var state = Guid.NewGuid().ToString();

        await Browser.NavigateTo(new Uri(tlogin.AuthorizationEndpoint +
                                         $"?response_type=code&client_id={tlogin.ClientID}&state={state}&scope={scopes}"));

        var uri = await handler.Task.WaitAsync(token);

        var cookies = await Browser.Cookies(tlogin.AuthorizationEndpoint.Host, token);

        var parsed = HttpUtility.ParseQueryString(uri.Query);
        if (parsed.Get("state") != state)
        {
            _logger.LogCritical("Bad OAuth state, this shouldn't happen");
            throw new Exception("Bad OAuth State");
        }

        if (parsed.Get("code") == null)
        {
            _logger.LogCritical("Bad code result from OAuth");
            throw new Exception("Bad code result from OAuth");
        }

        var authCode = parsed.Get("code");

        var formData = new KeyValuePair<string?, string?>[]
        {
            new("grant_type", "authorization_code"),
            new("code", authCode),
            new("client_id", tlogin.ClientID)
        };

        var msg = new HttpRequestMessage();
        msg.Method = HttpMethod.Post;
        msg.RequestUri = tlogin.TokenEndpoint;
        msg.Headers.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/92.0.4515.159 Safari/537.36");
        msg.Headers.Add("Cookie", string.Join(";", cookies.Select(c => $"{c.Name}={c.Value}")));
        msg.Content = new FormUrlEncodedContent(formData.ToList());

        using var response = await _httpClient.SendAsync(msg, token);
        var data = await response.Content.ReadFromJsonAsync<OAuthResultState>(cancellationToken: token);

        await _tokenProvider.SetToken(new TLoginType
        {
            Cookies = cookies,
            ResultState = data!
        });
    }
    
    private class AsyncSchemeHandler : CefSchemeHandlerFactory
    {
        private readonly TaskCompletionSource<Uri> _tcs = new();

        public Task<Uri> Task => _tcs.Task;

        protected override CefResourceHandler Create(CefBrowser browser, CefFrame frame, string schemeName,
            CefRequest request)
        {
            return new Handler(_tcs);
        }
    }

    private class Handler : CefResourceHandler
    {
        private readonly TaskCompletionSource<Uri> _tcs;

        public Handler(TaskCompletionSource<Uri> tcs)
        {
            _tcs = tcs;
        }

        protected override bool ProcessRequest(CefRequest request, CefCallback callback)
        {
            _tcs.TrySetResult(new Uri(request.Url));
            return false;
        }
    }
}