#nullable enable
using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.DTOs.OAuth;

namespace Wabbajack.Networking.NexusApi.OAuth;

/// <summary>How a login ended. Only <see cref="Succeeded" /> carries a token.</summary>
public enum NexusOAuthOutcome
{
    Succeeded,
    /// <summary>The caller cancelled, which is the user changing their mind.</summary>
    Cancelled,
    /// <summary>The browser was opened and nothing ever came back.</summary>
    TimedOut,
    /// <summary>Nexus Mods redirected with an error rather than a code, which <c>access_denied</c> is.</summary>
    Denied,
    /// <summary>A login was already in flight, and this one was not started.</summary>
    AlreadyRunning,
    Failed
}

/// <summary>
///     What a login produced, or why it produced nothing. <see cref="Detail" /> is a sentence for the log
///     or the user; it is null only on success.
/// </summary>
public sealed record NexusOAuthResult(NexusOAuthOutcome Outcome, JwtTokenReply? Token, string? Detail)
{
    public static NexusOAuthResult Success(JwtTokenReply token)
    {
        return new NexusOAuthResult(NexusOAuthOutcome.Succeeded, token, null);
    }

    public static NexusOAuthResult Problem(NexusOAuthOutcome outcome, string detail)
    {
        return new NexusOAuthResult(outcome, null, detail);
    }
}

/// <summary>
///     The Nexus Mods login, as RFC 8252 describes one for a native app: the authorization request goes to
///     the browser the user already has, and the redirect comes back to a loopback server this process runs
///     for as long as the login takes.
///     <para>
///         This used to be an embedded WebView2 with its <c>NewWindowRequested</c> event hooked, filtering
///         for a host of 127.0.0.1 and reading the code straight out of the URL it was about to navigate to.
///         No HTTP request was ever made and nothing listened on the redirect - the interception was the
///         redirect handler - so the redirect URI was whatever the filter matched rather than an address.
///         A real loopback server is the thing that filter was standing in for.
///     </para>
/// </summary>
public class NexusOAuthLogin
{
    public static readonly Uri OAuthUrl = new("https://users.nexusmods.com/oauth");
    public const string ClientId = "wabbajack";

    /// <summary>
    ///     The path half of the redirect URI. Nexus Mods has <c>http://localhost:*/oauth/callback</c>
    ///     registered for this client, so the port may be anything and the rest may not.
    /// </summary>
    public const string CallbackPath = "/oauth/callback";

    /// <summary>
    ///     How long the loopback server waits for a redirect. Long enough to cover finding a password,
    ///     answering two-factor and reading the consent screen; short enough that a user who closed the
    ///     browser and forgot gets their socket back the same day.
    /// </summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(10);

    /// <summary>
    ///     How long the token exchange gets. Generous for one HTTP round trip and short enough that a user
    ///     watching a button is told something: the whole wait before this belongs to a person deciding
    ///     whether to sign in, while this one is two machines talking, and nothing on screen says it is
    ///     happening.
    /// </summary>
    public static readonly TimeSpan ExchangeTimeout = TimeSpan.FromSeconds(60);

    private readonly HttpClient _client;
    private readonly ILogger<NexusOAuthLogin> _logger;

    /// <summary>
    ///     One login at a time. Two would bind two ports, open two browser tabs, and leave the second
    ///     waiting on a redirect the first had already spent - so the login the user actually finished would
    ///     look to them like the one that timed out.
    /// </summary>
    private readonly SemaphoreSlim _oneAtATime = new(1, 1);

    private readonly TimeSpan _timeout;

    public NexusOAuthLogin(ILogger<NexusOAuthLogin> logger, HttpClient client)
        : this(logger, client, DefaultTimeout)
    {
    }

    /// <summary>The same, with a shorter wait for the browser - which is what the tests need.</summary>
    public NexusOAuthLogin(ILogger<NexusOAuthLogin> logger, HttpClient client, TimeSpan timeout)
    {
        _logger = logger;
        _client = client;
        _timeout = timeout;
    }

    /// <summary>
    ///     Runs the whole authorization-code flow and hands back whatever Nexus Mods returned, or the reason
    ///     there is nothing to hand back.
    ///     <para>
    ///         Never throws. One caller is a WPF <c>async void</c> message handler, where an exception is an
    ///         unhandled one that takes the app down; the other is a CLI verb. Everything that can go wrong
    ///         here - a browser that never came back, a redirect carrying an error, a token endpoint that
    ///         refused - is an outcome rather than an exception.
    ///     </para>
    /// </summary>
    /// <param name="openBrowser">
    ///     Hands the authorize URL to the system browser. Taken as a parameter because opening a browser is
    ///     the host's business, and because a test needs somewhere to answer from.
    /// </param>
    public async Task<NexusOAuthResult> LogIn(Action<Uri> openBrowser, CancellationToken token)
    {
        if (!_oneAtATime.Wait(0))
            return NexusOAuthResult.Problem(NexusOAuthOutcome.AlreadyRunning,
                "A Nexus Mods login is already in progress");

        try
        {
            return await Run(openBrowser, token);
        }
        catch (OperationCanceledException)
        {
            return token.IsCancellationRequested
                ? NexusOAuthResult.Problem(NexusOAuthOutcome.Cancelled, "The Nexus Mods login was cancelled")
                : NexusOAuthResult.Problem(NexusOAuthOutcome.TimedOut,
                    $"Nexus Mods did not come back within {_timeout.TotalMinutes:0} minutes");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "The Nexus Mods login failed");
            return NexusOAuthResult.Problem(NexusOAuthOutcome.Failed, ex.Message);
        }
        finally
        {
            _oneAtATime.Release();
        }
    }

    private async Task<NexusOAuthResult> Run(Action<Uri> openBrowser, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        using var listener = new OAuthLoopbackListener(CallbackPath);

        // see https://www.rfc-editor.org/rfc/rfc7636#section-4.1
        var codeVerifier = Convert.ToBase64String(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString("N")));

        // see https://www.rfc-editor.org/rfc/rfc7636#section-4.2
        var codeChallenge = Base64Url.EncodeToString(SHA256.HashData(Encoding.UTF8.GetBytes(codeVerifier)));

        var state = Guid.NewGuid().ToString();
        var authorize = GenerateAuthorizeUrl(listener.RedirectUri, codeChallenge, state);

        _logger.LogInformation("Opening the Nexus Mods login in your browser; it comes back to {RedirectUri}",
            listener.RedirectUri);

        // The whole authorize URL, because the only thing that can reject it is the authorization server and
        // the only person who can see why is whoever holds the client registration. "Malformed or doesn't
        // match client redirect URI" says nothing about which of the two it was, and Nexus Mods validates
        // the redirect only after the user has signed in, so this line is the one record of what was
        // actually asked for. Neither the verifier nor the token is in it - a challenge and a state are
        // safe to write down, and both die with this attempt.
        // AbsoluteUri, never ToString(): ToString() unescapes what it can, so the redirect URI in it reads
        // "http://localhost:57957/oauth/callback" where the browser is actually handed
        // "http%3A%2F%2Flocalhost%3A57957%2Foauth%2Fcallback". Logging the unescaped one would show a
        // malformed request that was never sent, while hiding a genuinely malformed one.
        _logger.LogDebug("Authorize URL: {Authorize}", authorize.AbsoluteUri);

        using var waiting = CancellationTokenSource.CreateLinkedTokenSource(token);
        waiting.CancelAfter(_timeout);

        // Accepting starts before the browser is opened, and the order matters more than it looks.
        // Binding the socket is not the same as being ready to answer on it: a bound, listening socket has
        // the kernel completing the browser's TCP handshake into the accept queue whether or not anything
        // has called Accept, so a redirect that arrives first does not fail - it sits there, connected,
        // waiting for a response that only arrives once someone accepts. Opening the browser first made
        // that a real wait rather than a theoretical one, because opening it is ShellExecute on this very
        // thread, and a cold browser start holds the thread that was going to do the accepting. What the
        // user saw was a tab spinning against a port that was demonstrably listening.
        //
        // Calling this without awaiting it runs it up to its first await, which is the accept, so by the
        // time openBrowser is reached the accept is registered and its completion belongs to the thread
        // pool. Whatever the browser does next, something is waiting for it.
        var arriving = listener.WaitForCallback(waiting.Token);

        OpenInBackground(openBrowser, authorize);

        using var callback = await arriving;

        var rejection = Rejection(callback.Query, state);

        // The page goes out before the token exchange, so the browser is not left spinning while this talks
        // to Nexus Mods. It only ever reports what the redirect itself said; a refusal from the token
        // endpoint after this is the app's to report, and the app is where the user has just been sent.
        await callback.Respond(CallbackPage.For(rejection), CancellationToken.None);

        if (rejection != null)
        {
            _logger.LogWarning("The Nexus Mods redirect did not carry a login: {Detail}", rejection.Detail);
            return rejection;
        }

        _logger.LogInformation("Nexus Mods returned an authorization code; exchanging it for a token");

        // Byte for byte what went out in the authorize request. Nexus Mods compares the two and refuses the
        // exchange if they differ, so this reads the listener again rather than rebuilding the string.
        //
        // Bounded on its own, because this is the one step with nobody watching it. The user has been told
        // "authorization successful" by the page and has gone back to the app, so an exchange that hangs is
        // a login that never finishes, never logs and never re-arms its own button - which is what it looked
        // like. The HttpClient here is shared, so its timeout is not this call's to set.
        using var exchanging = CancellationTokenSource.CreateLinkedTokenSource(token);
        exchanging.CancelAfter(ExchangeTimeout);

        JwtTokenReply? received;
        try
        {
            received = await AuthorizeToken(listener.RedirectUri, codeVerifier, callback.Query["code"],
                exchanging.Token);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            return NexusOAuthResult.Problem(NexusOAuthOutcome.Failed,
                $"Nexus Mods did not answer the token request within {ExchangeTimeout.TotalSeconds:N0} seconds");
        }

        return received == null
            ? NexusOAuthResult.Problem(NexusOAuthOutcome.Failed, "Nexus Mods did not return a token")
            : NexusOAuthResult.Success(received);
    }

    /// <summary>
    ///     Null when the redirect carried a code this login can use; otherwise why it did not.
    ///     <para>
    ///         The state is what ties a redirect to the request that started it, so a redirect carrying the
    ///         wrong one is not this login's and nothing else in it is read. One carrying none at all is
    ///         trusted only far enough to report an error with, since an error is the one thing RFC 6749
    ///         allows a server to send back before it has understood the request.
    ///     </para>
    /// </summary>
    public static NexusOAuthResult? Rejection(IReadOnlyDictionary<string, string> query, string expectedState)
    {
        query.TryGetValue("state", out var state);
        var matches = string.Equals(state, expectedState, StringComparison.Ordinal);

        if (state != null && !matches)
            return NexusOAuthResult.Problem(NexusOAuthOutcome.Failed,
                "The Nexus Mods redirect did not belong to this login");

        if (query.TryGetValue("error", out var error))
        {
            query.TryGetValue("error_description", out var description);
            return NexusOAuthResult.Problem(NexusOAuthOutcome.Denied,
                string.IsNullOrWhiteSpace(description) ? error : description);
        }

        if (!matches)
            return NexusOAuthResult.Problem(NexusOAuthOutcome.Failed,
                "The Nexus Mods redirect did not belong to this login");

        if (!query.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code))
            return NexusOAuthResult.Problem(NexusOAuthOutcome.Failed,
                "The Nexus Mods redirect carried no authorization code");

        return null;
    }

    /// <summary>
    ///     Hands the authorize URL to the browser on a thread of its own, and does not wait for it.
    ///     <para>
    ///         Opening a URL means <c>ShellExecute</c>, which is documented as wanting an STA thread and is
    ///         free to take as long as the shell takes - a cold browser start, a default-handler lookup, a
    ///         browser mid-update. Called inline from this flow it did worse than take its time: on a
    ///         thread-pool thread, which is MTA, it did not come back at all. The redirect then arrived at a
    ///         socket that was bound and listening, so the kernel completed the handshake into the accept
    ///         queue, and the browser sat connected to a port nothing was reading, waiting for a response
    ///         that could only be written after the call that was stuck. A tab spinning against a listening
    ///         port, and a login button that would not arm again because its flow had never finished.
    ///     </para>
    ///     <para>
    ///         So it gets an STA thread, and the flow does not depend on it returning. If it never does, the
    ///         redirect is still accepted and answered, and the login still completes. If the browser truly
    ///         never opened, the user sees nothing happen and the wait times out, which is the honest
    ///         outcome for that. The thread is a background one so a stuck <c>ShellExecute</c> cannot keep
    ///         the process alive.
    ///     </para>
    /// </summary>
    private void OpenInBackground(Action<Uri> openBrowser, Uri authorize)
    {
        var thread = new Thread(() =>
        {
            try
            {
                openBrowser(authorize);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not open the Nexus Mods login in a browser");
            }
        })
        {
            IsBackground = true,
            Name = "Nexus login browser"
        };

        if (OperatingSystem.IsWindows()) thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    /// <summary>
    ///     Trades the authorization code for a token, or null when Nexus Mods refused. A refusal is not
    ///     thrown and, just as importantly, is not a login: what the caller does with a null is leave
    ///     whatever was already stored exactly where it is.
    /// </summary>
    public async Task<JwtTokenReply?> AuthorizeToken(Uri redirectUri, string verifier, string code,
        CancellationToken token)
    {
        var request = new Dictionary<string, string>
        {
            {"grant_type", "authorization_code"},
            {"client_id", ClientId},
            {"redirect_uri", redirectUri.ToString()},
            {"code", code},
            {"code_verifier", verifier}
        };

        var response = await _client.PostAsync($"{OAuthUrl}/token", new FormUrlEncodedContent(request), token);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogCritical("Failed to get token {Code} - {Message}", response.StatusCode,
                response.ReasonPhrase);
            return null;
        }

        var responseString = await response.Content.ReadAsStringAsync(token);
        try
        {
            return JsonSerializer.Deserialize<JwtTokenReply>(responseString);
        }
        catch (JsonException ex)
        {
            // A body that is not the reply we asked for is a refusal like any other.
            _logger.LogCritical(ex, "Nexus Mods returned something that is not a token");
            return null;
        }
    }

    public static Uri GenerateAuthorizeUrl(Uri redirectUri, string challenge, string state)
    {
        var request = new Dictionary<string, string>
        {
            {"response_type", "code"},
            {"scope", "public openid profile"},
            {"code_challenge_method", "S256"},
            {"client_id", ClientId},
            {"redirect_uri", redirectUri.ToString()},
            {"code_challenge", challenge},
            {"state", state}
        };

        return OAuthQuery.AddTo($"{OAuthUrl}/authorize", request);
    }
}
