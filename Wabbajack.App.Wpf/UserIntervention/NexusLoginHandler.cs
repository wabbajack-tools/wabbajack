using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reactive.Disposables;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using ReactiveUI;
using Wabbajack.DTOs.Logins;
using Wabbajack.DTOs.OAuth;
using Wabbajack.Messages;
using Wabbajack.Services.OSIntegrated;

namespace Wabbajack.UserIntervention;

public class NexusLoginHandler : BrowserWindowViewModel
{
    private readonly EncryptedJsonTokenProvider<NexusOAuthState> _tokenProvider;
    private readonly ILogger<NexusLoginHandler> _logger;
    private readonly HttpClient _client;
    private readonly NexusAuthHelper _nexusAuthHelper;

    public NexusLoginHandler(ILogger<NexusLoginHandler> logger, HttpClient client, IServiceProvider serviceProvider, NexusAuthHelper nexusAuthHelper) : base(serviceProvider)
    {
        _logger = logger;
        _client = client;
        _nexusAuthHelper = nexusAuthHelper;

        HeaderText = "Nexus Login";
        SupportsNativeWindow = true;

        OpenNativeWindowCommand = ReactiveCommand.Create(async () =>
        {
            HideWindowOnClose = false;
            await _tokenSource.CancelAsync();
            ShowFloatingWindow.Send(FloatingScreenType.None);
            ShowFloatingWindow.Send(FloatingScreenType.NativeNexusLogin);
        });
    }

    protected override async Task Run(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        
        // see https://www.rfc-editor.org/rfc/rfc7636#section-4.1
        var codeVerifier = Guid.NewGuid().ToString("N").ToBase64();

        // see https://www.rfc-editor.org/rfc/rfc7636#section-4.2
        var codeChallengeBytes = SHA256.HashData(Encoding.UTF8.GetBytes(codeVerifier));
        var codeChallenge = StringBase64Extensions.Base64UrlEncode(codeChallengeBytes);

        Instructions = "Please log into the Nexus";

        var state = Guid.NewGuid().ToString();
        
        await NavigateTo(new Uri("https://nexusmods.com"), token);
        token.ThrowIfCancellationRequested();
        var codeCompletionSource = new TaskCompletionSource<Dictionary<string, StringValues>>();
        
        Browser.CoreWebView2.NewWindowRequested += (sender, args) =>
        {
            var uri = new Uri(args.Uri);
            _logger.LogInformation("New Window Requested {Uri}", args.Uri);
            if (uri.Host != "127.0.0.1") return;
            
            codeCompletionSource.TrySetResult(QueryHelpers.ParseQuery(uri.Query));
            args.Handled = true;
        };

        var uri = _nexusAuthHelper.GenerateAuthorizeUrl(codeChallenge, state);
        await NavigateTo(uri, token);

        var ctx = await codeCompletionSource.Task.WaitAsync(token);
        
        if (ctx["state"].FirstOrDefault() != state)
        {
            throw new Exception("State mismatch");
        }
        
        var code = ctx["code"].FirstOrDefault();

        var result = await _nexusAuthHelper.AuthorizeToken(codeVerifier, code, token);
        
        if (result != null) 
            result.ReceivedAt = DateTime.UtcNow.ToFileTimeUtc();

        await _tokenProvider.SetToken(new NexusOAuthState()
        {
            OAuth = result!
        });
    }
}