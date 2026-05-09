using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Reactive.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Wabbajack.Downloaders;
using Wabbajack.DTOs.Logins;
using Wabbajack.DTOs.OAuth;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.UserIntervention;

namespace Wabbajack.LoginManagers;

public partial class NexusLoginManager : ViewModel, ILoginFor<NexusDownloader>
{
    private const string OAuthUrl = "https://users.nexusmods.com/oauth";
    private const string OAuthRedirectUrl = "https://127.0.0.1:1234";
    private const string OAuthClientId = "wabbajack";
    private const int ListenerPort = 1234;

    private readonly ILogger<NexusLoginManager> _logger;
    private readonly ITokenProvider<NexusOAuthState> _token;
    private readonly HttpClient _httpClient;

    public string SiteName { get; } = "Nexus Mods";
    public ICommand TriggerLogin { get; set; }
    public ICommand ClearLogin { get; set; }
    public ICommand ToggleLogin { get; set; }

    public ImageSource Icon { get; set; }
    public Type LoginFor()
    {
        return typeof(NexusDownloader);
    }

    [Reactive]
    public partial bool LoggedIn { get; set; }

    public NexusLoginManager(ILogger<NexusLoginManager> logger, ITokenProvider<NexusOAuthState> token, HttpClient httpClient)
    {
        _logger = logger;
        _token = token;
        _httpClient = httpClient;
        Task.Run(RefreshTokenState);

        ClearLogin = ReactiveCommand.CreateFromTask(async () =>
        {
            _logger.LogInformation("Deleting Login information for {SiteName}", SiteName);
            await ClearLoginToken();
        }, this.WhenAnyValue(v => v.LoggedIn));

        Icon = (DrawingImage)Application.Current.Resources["NexusLogo"];

        TriggerLogin = ReactiveCommand.CreateFromTask(async () =>
        {
            _logger.LogInformation("Logging into {SiteName}", SiteName);
            await StartLogin();
        }, this.WhenAnyValue(v => v.LoggedIn).Select(v => !v));

        ToggleLogin = ReactiveCommand.Create(() =>
        {
            if (LoggedIn) ClearLogin.Execute(null);
            else TriggerLogin.Execute(null);
        });
    }

    private async Task ClearLoginToken()
    {
        await _token.Delete();
        await RefreshTokenState();
    }

    private async Task StartLogin()
    {
        // PKCE: generate code verifier and challenge (RFC 7636)
        var codeVerifier = Guid.NewGuid().ToString("N").ToBase64();
        var codeChallengeBytes = SHA256.HashData(Encoding.UTF8.GetBytes(codeVerifier));
        var codeChallenge = StringBase64Extensions.Base64UrlEncode(codeChallengeBytes);

        var state = Guid.NewGuid().ToString();

        // Generate a self-signed cert for the HTTPS listener (lives only in memory)
        using var cert = GenerateSelfSignedCert();
        var tcpListener = new TcpListener(IPAddress.Loopback, ListenerPort);

        try
        {
            tcpListener.Start();
            _logger.LogInformation("OAuth HTTPS listener started on port {Port}", ListenerPort);

            // Open the system browser to the authorization URL
            var authorizeUrl = GenerateAuthorizeUrl(codeChallenge, state);
            Process.Start(new ProcessStartInfo(authorizeUrl.ToString()) { UseShellExecute = true });

            // Accept connections in a loop until we get the auth code or timeout.
            // Browsers may make multiple connection attempts (preconnect, favicon, etc.)
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

            while (!cts.Token.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await tcpListener.AcceptTcpClientAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Nexus OAuth login timed out after 5 minutes");
                    return;
                }

                try
                {
                    using (client)
                    {
                        var result = await HandleOAuthCallback(client, cert, state, codeVerifier, cts.Token);
                        if (result)
                            return; // Successfully processed the auth callback
                    }
                }
                catch (Exception ex)
                {
                    // TLS handshake failures, malformed requests, etc. — keep listening
                    _logger.LogDebug(ex, "OAuth listener: connection attempt failed, continuing to listen");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to complete Nexus OAuth login");
        }
        finally
        {
            tcpListener.Stop();
            await RefreshTokenState();
        }
    }

    /// <summary>
    /// Handle a single incoming connection. Returns true if the OAuth callback was successfully processed.
    /// </summary>
    private async Task<bool> HandleOAuthCallback(TcpClient client, X509Certificate2 cert, string state, string codeVerifier, CancellationToken token)
    {
        await using var sslStream = new SslStream(client.GetStream(), false);
        await sslStream.AuthenticateAsServerAsync(cert);

        // Read the HTTP request
        var buffer = new byte[8192];
        var bytesRead = await sslStream.ReadAsync(buffer, token);
        var requestText = Encoding.UTF8.GetString(buffer, 0, bytesRead);

        _logger.LogDebug("OAuth listener received request: {Request}", requestText.Split('\r', '\n')[0]);

        // Parse the query string from "GET /?code=...&state=... HTTP/1.1"
        var match = Regex.Match(requestText, @"^GET /\?(\S+) HTTP");
        if (!match.Success)
        {
            // Not the callback we're looking for (e.g., favicon request) — send 404 and keep listening
            await SendSslResponse(sslStream, "Not found", 404);
            return false;
        }

        var queryParams = QueryHelpers.ParseQuery(match.Groups[1].Value);

        // If no code parameter, this isn't the OAuth callback
        if (!queryParams.ContainsKey("code") && !queryParams.ContainsKey("error"))
        {
            await SendSslResponse(sslStream, "Not found", 404);
            return false;
        }

        // Validate state parameter
        var returnedState = queryParams["state"].FirstOrDefault();
        if (returnedState != state)
        {
            _logger.LogError("OAuth state mismatch: expected {Expected}, got {Actual}", state, returnedState);
            await SendSslResponse(sslStream, "Login failed: state mismatch. Please try again.");
            return true; // Stop listening — this was a real callback, just invalid
        }

        // Check for error response
        if (queryParams.ContainsKey("error"))
        {
            _logger.LogError("OAuth error: {Error}", queryParams["error"].FirstOrDefault());
            await SendSslResponse(sslStream, "Login failed. Please try again.");
            return true;
        }

        var code = queryParams["code"].FirstOrDefault();
        if (string.IsNullOrEmpty(code))
        {
            _logger.LogError("OAuth callback did not contain an authorization code");
            await SendSslResponse(sslStream, "Login failed: no authorization code received.");
            return true;
        }

        // Exchange the authorization code for a token
        var tokenResult = await ExchangeCodeForToken(codeVerifier, code);
        if (tokenResult == null)
        {
            await SendSslResponse(sslStream, "Login failed: could not exchange code for token.");
            return true;
        }

        tokenResult.ReceivedAt = DateTime.UtcNow.ToFileTimeUtc();
        await _token.SetToken(new NexusOAuthState { OAuth = tokenResult });
        await SendSslResponse(sslStream, "Login complete! You can close this tab and return to Wabbajack.");
        _logger.LogInformation("Nexus OAuth login completed successfully");
        return true;
    }

    private static X509Certificate2 GenerateSelfSignedCert()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Wabbajack OAuth Localhost",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        // Key usage extensions required by Windows SChannel for TLS server auth
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                critical: true));

        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                new OidCollection { new("1.3.6.1.5.5.7.3.1") }, // serverAuth OID
                critical: false));

        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(sanBuilder.Build());

        // Short-lived cert, only needs to survive the login flow
        using var cert = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow.AddMinutes(10));

        // Export and re-import with Exportable flag so SChannel can access the private key
        return new X509Certificate2(
            cert.Export(X509ContentType.Pfx, ""),
            "",
            X509KeyStorageFlags.Exportable);
    }

    private static async Task SendSslResponse(SslStream sslStream, string message, int statusCode = 200)
    {
        var statusText = statusCode == 200 ? "OK" : "Not Found";
        var html = $"<html><body><h1>{message}</h1></body></html>";
        var body = Encoding.UTF8.GetBytes(html);
        var header = $"HTTP/1.1 {statusCode} {statusText}\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n";
        await sslStream.WriteAsync(Encoding.UTF8.GetBytes(header));
        await sslStream.WriteAsync(body);
        await sslStream.FlushAsync();
    }

    private async Task<JwtTokenReply?> ExchangeCodeForToken(string codeVerifier, string code)
    {
        var request = new Dictionary<string, string>
        {
            { "grant_type", "authorization_code" },
            { "client_id", OAuthClientId },
            { "redirect_uri", OAuthRedirectUrl },
            { "code", code },
            { "code_verifier", codeVerifier },
        };

        var content = new FormUrlEncodedContent(request);
        var response = await _httpClient.PostAsync($"{OAuthUrl}/token", content);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to exchange code for token: {StatusCode} - {Reason}",
                response.StatusCode, response.ReasonPhrase);
            return null;
        }

        var responseString = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JwtTokenReply>(responseString);
    }

    private static Uri GenerateAuthorizeUrl(string challenge, string state)
    {
        var request = new Dictionary<string, string>
        {
            { "response_type", "code" },
            { "scope", "public openid profile" },
            { "code_challenge_method", "S256" },
            { "client_id", OAuthClientId },
            { "redirect_uri", OAuthRedirectUrl },
            { "code_challenge", challenge },
            { "state", state },
        };

        return new Uri(QueryHelpers.AddQueryString($"{OAuthUrl}/authorize", request));
    }

    private async Task RefreshTokenState()
    {
        NexusOAuthState token = null;
        try
        {
            token = await _token.Get();
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to refresh Nexus token state: {ex}", ex.ToString());
        }

        LoggedIn = _token.HaveToken() && !(token?.OAuth?.IsExpired ?? true);
    }
}
