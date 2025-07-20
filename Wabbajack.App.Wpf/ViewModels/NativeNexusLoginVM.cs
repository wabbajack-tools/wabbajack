using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reactive.Disposables;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CG.Web.MegaApiClient;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.Common;
using Wabbajack.DTOs.Logins;
using Wabbajack.Messages;
using Wabbajack.Networking.NexusApi;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Services.OSIntegrated;
using Wabbajack.Services.OSIntegrated.TokenProviders;
using Wabbajack.UserIntervention;

namespace Wabbajack;

public class NativeNexusLoginVM : ViewModel
{
    private readonly ILogger<NativeNexusLoginVM> _logger;
    private readonly HttpClient _client;
    private readonly EncryptedJsonTokenProvider<NexusOAuthState> _tokenProvider;
    private readonly NexusAuthHelper _nexusAuthHelper;
    private readonly NexusApi _api;

    private enum LoginType
    {
        Normal,
        TwoFactor
    }

    public ICommand CloseCommand { get; }
    public ICommand LoginCommand { get; }
    public ICommand TwoFactorLoginCommand { get; }

    [Reactive] public string Email { get; set; }
    [Reactive] public string Password { get; set; }

    [Reactive] public bool LoggingIn { get; set; }
    [Reactive] public bool LoginSuccessful { get; set; }
    [Reactive] public bool TriedLoggingIn { get; set; }

    [Reactive] public bool TriedLoggingInWithTwoFactor { get; set; }
    [Reactive] public bool TwoFactorLoginRequested { get; set; }
    [Reactive] public string TwoFactorKey { get; set; }

    public NativeNexusLoginVM(
        ILogger<NativeNexusLoginVM> logger,
        HttpClient client,
        EncryptedJsonTokenProvider<NexusOAuthState> tokenProvider,
        NexusAuthHelper nexusAuthHelper,
        NexusApi api)
    {
        _logger = logger;
        _client = client;
        _tokenProvider = tokenProvider;
        _nexusAuthHelper = nexusAuthHelper;
        _api = api;

        CloseCommand = ReactiveCommand.Create(async () =>
        {
            ShowFloatingWindow.Send(FloatingScreenType.None);
        });

        LoginCommand = ReactiveCommand.Create(async (LoginType loginType) =>
        {
            // see https://www.rfc-editor.org/rfc/rfc7636#section-4.1
            var codeVerifier = Guid.NewGuid().ToString("N").ToBase64();

            // see https://www.rfc-editor.org/rfc/rfc7636#section-4.2
            var codeChallengeBytes = SHA256.HashData(Encoding.UTF8.GetBytes(codeVerifier));
            var codeChallenge = StringBase64Extensions.Base64UrlEncode(codeChallengeBytes);

            var state = Guid.NewGuid().ToString();
            var codeCompletionSource = new TaskCompletionSource<Dictionary<string, StringValues>>();

            TriedLoggingIn = true;
            LoggingIn = true;
            // Since the login task can gets stuck on a failed login, cancel the login task if it hasn't returned after 30s
            using var tokenSource = new CancellationTokenSource();
            tokenSource.CancelAfter(TimeSpan.FromSeconds(30));
            try
            {
                if (loginType == LoginType.Normal)
                {
                    var uri = _nexusAuthHelper.GenerateAuthorizeUrl(codeChallenge, state);
                    var message = new HttpRequestMessage(HttpMethod.Get, uri);
                    message.Headers.Add("User-Agent", ["Wabbajack"]);
                    var response = await _client.SendAsync(message);
                    response.EnsureSuccessStatusCode();
                    string responseContent = await response.Content.ReadAsStringAsync();

                    var doc = new HtmlDocument();
                    doc.LoadHtml(responseContent);

                    // Get the login form
                    /*
                    var loginForm = doc.DocumentNode.SelectSingleNode("//form");

                    var hiddenFields = doc.DocumentNode.SelectNodes("//input[@type='hidden']");
                    var formData = new Dictionary<string, string>
                    {
                        { "username", username },
                        { "password", password }
                    };
                    */
                    //var response = await _api.Send<string>(message);
                    /*
                    _client.SendAsync();
                    */
                    //var (auth, loginToken) = await DoLogin(tokenSource.Token);
                    /*
                    LoggedIntoMega.Send(auth);
                    */
                }
                else if (loginType == LoginType.TwoFactor)
                {
                    /*
                    TriedLoggingInWithTwoFactor = true;
                    var (auth, loginToken) = await DoTwoFactorLogin(tokenSource.Token);
                    LoggedIntoMega.Send(auth);
                    */
                }
                LoginSuccessful = true;

                // To show the user they're logged in before closing
                await Task.Delay(500);
                CloseCommand.Execute(null);
            }
            catch (Exception ex)
            {
                if (ex is OperationCanceledException)
                    _logger.LogError("Request timed out, MEGA login cancelled!");

                if (ex is ApiException apiEx && apiEx.ApiResultCode == ApiResultCode.TwoFactorAuthenticationError)
                {
                    _logger.LogInformation("Requesting MEGA 2FA login...");
                    TwoFactorLoginRequested = true;
                }
                else
                    _logger.LogError("Failed to log into MEGA: {ex}", ex.ToString());
                LoginSuccessful = false;
            }
            finally
            {
                LoggingIn = false;
            }
        }, this.WhenAnyValue(vm => vm.LoggingIn, loggingIn => !loggingIn));

        TwoFactorLoginCommand = ReactiveCommand.Create(() => LoginCommand.Execute(LoginType.TwoFactor), this.WhenAnyValue(vm => vm.LoggingIn, loggingIn => !loggingIn));

        this.WhenActivated(disposables =>
        {
            _logger.LogInformation("User attempting to sign into MEGA");

            Email = "";
            Password = "";
            LoggingIn = false;
            LoginSuccessful = false;
            TriedLoggingIn = false;

            TwoFactorKey = "";
            TwoFactorLoginRequested = false;
            TriedLoggingInWithTwoFactor = false;

            Disposable.Empty.DisposeWith(disposables);
        });
    }

    /*
    private async Task<(AuthInfos, LogonSessionToken)> DoLogin(CancellationToken token)
    {
        var auth = await _apiClient.GenerateAuthInfosAsync(Email, Password).WaitAsync(token);
        var login = await _apiClient.LoginAsync(auth).WaitAsync(token);
        return (auth, login);
    }

    private async Task<(AuthInfos, LogonSessionToken)> DoTwoFactorLogin(CancellationToken token)
    {
        var auth = await _apiClient.GenerateAuthInfosAsync(Email, Password, TwoFactorKey).WaitAsync(token);
        var login = await _apiClient.LoginAsync(auth).WaitAsync(token);
        return (auth, login);
    }
    */

}
