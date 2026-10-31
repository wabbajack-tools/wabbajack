using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using SteamKit2;
using SteamKit2.Authentication;
using Wabbajack.DTOs.Logins;
using Wabbajack.Networking.Http.Interfaces;

namespace Wabbajack.Networking.Steam;

public class SteamSession : ISteamSession
{
    /// <summary>
    ///     Shown on the Steam approval sheet and in the user's list of authorised devices. Being honest about
    ///     who is asking is the whole point of the name.
    /// </summary>
    private const string DeviceFriendlyName = "Wabbajack";

    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan LogOnTimeout = TimeSpan.FromSeconds(60);

    private readonly SteamClient _client;
    private readonly ILogger<SteamSession> _logger;

    /// <summary>
    ///     Steam identifies a session by the pair (public IP, LoginID) and refuses two sessions on one account
    ///     sharing both. Left unset, SteamKit computes the LoginID from the machine's primary bind address as
    ///     <c>localIP ^ 0xBAADF00D</c> -- a pure function of the machine, so every client on it lands on the
    ///     same value, including the user's own Steam client. Logging in with it disconnects them from Steam,
    ///     which reads as "Wabbajack broke my Steam". So this is a correctness requirement rather than a
    ///     nicety, and any value that is ours alone will do.
    /// </summary>
    private readonly uint _loginId = GenerateLoginId();

    private readonly SemaphoreSlim _loginLock = new(1, 1);
    private readonly CallbackManager _manager;
    private readonly ISteamGuardPrompt _prompt;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly SteamApps _steamApps;
    private readonly SteamContent _steamContent;
    private readonly SteamUser _steamUser;
    private readonly ITokenProvider<SteamLoginState> _tokenProvider;

    private TaskCompletionSource<bool>? _connected;
    private bool _disposed;

    /// <summary>
    ///     Completed when Steam has sent the account's licences, which it does shortly after logon rather
    ///     than as part of it. Anything deciding entitlement has to wait on this or it will read an empty
    ///     list and conclude the account owns nothing.
    /// </summary>
    private TaskCompletionSource<bool> _licensesReceived = NewLicenseSignal();

    private TaskCompletionSource<SteamUser.LoggedOnCallback>? _loggedOn;

    /// <summary>1 once the callback pump thread has been started; see <see cref="EnsurePump" />.</summary>
    private int _pumping;

    public SteamSession(ILogger<SteamSession> logger, ITokenProvider<SteamLoginState> tokenProvider,
        ISteamGuardPrompt prompt)
    {
        _logger = logger;
        _tokenProvider = tokenProvider;
        _prompt = prompt;

        _client = new SteamClient(SteamConfiguration.Create(c =>
        {
            c.WithProtocolTypes(ProtocolTypes.WebSocket);
            c.WithUniverse(EUniverse.Public);
        }));

        _manager = new CallbackManager(_client);
        _steamUser = _client.GetHandler<SteamUser>()!;
        _steamApps = _client.GetHandler<SteamApps>()!;
        _steamContent = _client.GetHandler<SteamContent>()!;

        _manager.Subscribe<SteamClient.ConnectedCallback>(OnConnected);
        _manager.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected);
        _manager.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);
        _manager.Subscribe<SteamUser.LoggedOffCallback>(OnLoggedOff);
        _manager.Subscribe<SteamApps.LicenseListCallback>(OnLicenseList);
    }

    /// <summary>
    ///     The licences the logged in account holds. Empty until Steam sends them, shortly after logon.
    /// </summary>
    public IReadOnlyList<SteamApps.LicenseListCallback.License> Licenses { get; private set; } =
        Array.Empty<SteamApps.LicenseListCallback.License>();

    public SteamApps Apps => _steamApps;

    /// <summary>
    ///     The content handler: manifest request codes, CDN auth tokens and the SteamPipe server list.
    /// </summary>
    public SteamContent Content => _steamContent;

    public SteamConfiguration Configuration => _client.Configuration;

    /// <summary>
    ///     The cell Steam assigned this logon, which is its idea of where the machine is. Passing it to the
    ///     server directory is what gets a nearby CDN rather than an arbitrary one. Null until a logon.
    /// </summary>
    public uint? CellId { get; private set; }

    public bool IsLoggedIn { get; private set; }

    public string? AccountName { get; private set; }

    public bool HaveStoredToken => _tokenProvider.HaveToken();

    /// <summary>
    ///     A CDN client bound to this connection. It is the caller's to dispose, and it carries its own
    ///     HttpClient, so one per content client rather than one per download.
    /// </summary>
    public SteamKit2.CDN.Client CreateCdnClient()
    {
        return new SteamKit2.CDN.Client(_client);
    }

    /// <summary>
    ///     Waits for Steam to send the account's licences. Returns false on timeout rather than throwing: an
    ///     account can legitimately hold none, so "nothing arrived" and "nothing to send" look the same from
    ///     here and neither is a reason to fail a download outright.
    /// </summary>
    public async Task<bool> WaitForLicensesAsync(TimeSpan timeout, CancellationToken token)
    {
        try
        {
            return await _licensesReceived.Task.WaitAsync(timeout, token).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("Steam did not send the account's licences within {Seconds}s",
                timeout.TotalSeconds);
            return false;
        }
    }

    public async Task<SteamLoginResult> LoginWithStoredTokenAsync(CancellationToken token)
    {
        await EnterLoginAsync(token).ConfigureAwait(false);
        try
        {
            var state = await TryGetStoredStateAsync().ConfigureAwait(false);
            if (state == null || string.IsNullOrWhiteSpace(state.RefreshToken))
                throw new SteamLoginRequiredException("There is no saved Steam login");

            if (SteamRefreshToken.IsExpired(state.RefreshTokenExpiresAt, DateTimeOffset.UtcNow))
            {
                await ForgetStoredTokenAsync().ConfigureAwait(false);
                throw new SteamLoginRequiredException("The saved Steam login has expired, log in again");
            }

            await ConnectAsync(token).ConfigureAwait(false);
            var callback = await LogOnAsync(state.AccountName, state.RefreshToken, true, token).ConfigureAwait(false);
            return new SteamLoginResult(state.AccountName, callback.ClientSteamID, true,
                state.RefreshTokenExpiresAt);
        }
        catch
        {
            Teardown();
            throw;
        }
        finally
        {
            _loginLock.Release();
        }
    }

    public async Task<SteamLoginResult> LoginWithQrCodeAsync(Action<string> onChallengeUrl, CancellationToken token)
    {
        await EnterLoginAsync(token).ConfigureAwait(false);
        try
        {
            await ConnectAsync(token).ConfigureAwait(false);

            var details = new AuthSessionDetails
            {
                DeviceFriendlyName = DeviceFriendlyName,
                IsPersistentSession = true

                // Deliberately no Authenticator. A QR session's only confirmation type is the mobile app's
                // device confirmation, and SteamKit throws outright if an authenticator declines it.

                // Deliberately no GuardData either. Which account is scanning is not known until the poll
                // comes back, and guard data belongs to one account; there is nothing here it could suppress
                // anyway, since the confirmation is the scan itself.
            };

            var session = await _client.Authentication.BeginAuthSessionViaQRAsync(details).ConfigureAwait(false);

            // An assignable property rather than an event, so this is an assignment and not a subscription.
            // Steam rotates the URL as a side effect of polling, so every call after the first arrives on the
            // polling thread.
            session.ChallengeURLChanged = () => onChallengeUrl(session.ChallengeURL);
            onChallengeUrl(session.ChallengeURL);

            var poll = await session.PollingWaitForResultAsync(token).ConfigureAwait(false);
            return await CompleteLoginAsync(poll, token).ConfigureAwait(false);
        }
        catch (AuthenticationException ex)
        {
            Teardown();
            throw Translate(ex, false);
        }
        catch
        {
            Teardown();
            throw;
        }
        finally
        {
            _loginLock.Release();
        }
    }

    public async Task<SteamLoginResult> LoginWithCredentialsAsync(string username, string password,
        CancellationToken token)
    {
        await EnterLoginAsync(token).ConfigureAwait(false);
        try
        {
            await ConnectAsync(token).ConfigureAwait(false);
            var stored = await TryGetStoredStateAsync().ConfigureAwait(false);

            var details = new AuthSessionDetails
            {
                Username = username,
                Password = password,
                DeviceFriendlyName = DeviceFriendlyName,
                IsPersistentSession = true,
                Authenticator = new SteamAuthenticator(_prompt, token),

                // Only reuse guard data belonging to this account, or Steam rejects it.
                GuardData = string.Equals(stored?.AccountName, username, StringComparison.OrdinalIgnoreCase)
                    ? stored?.GuardData
                    : null

                // PlatformType and WebsiteID keep their defaults of SteamClient and "Client": depot access
                // needs a client-audience token and a web-audience one will not open a depot.
            };

            var session = await _client.Authentication.BeginAuthSessionViaCredentialsAsync(details)
                .ConfigureAwait(false);
            var poll = await session.PollingWaitForResultAsync(token).ConfigureAwait(false);
            return await CompleteLoginAsync(poll, token).ConfigureAwait(false);
        }
        catch (AuthenticationException ex)
        {
            Teardown();
            throw Translate(ex, true);
        }
        catch
        {
            Teardown();
            throw;
        }
        finally
        {
            _loginLock.Release();
        }
    }

    public async ValueTask<SteamLogoutResult> LogoutAsync()
    {
        // Under the login lock, or a login finishing at the same moment writes its token back over the one
        // this just deleted.
        await EnterLoginAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            if (_client.IsConnected)
                _steamUser.LogOff();

            Teardown();
            AccountName = null;
            Licenses = Array.Empty<SteamApps.LicenseListCallback.License>();

            var deleted = await _tokenProvider.Delete().ConfigureAwait(false);

            // HaveToken() also answers for the environment variable the provider falls back to, which
            // Delete() has no way to remove. Saying "logged out" while that keeps working would be a lie.
            if (_tokenProvider.HaveToken()) return SteamLogoutResult.HeldInEnvironment;

            return deleted ? SteamLogoutResult.Deleted : SteamLogoutResult.NothingStored;
        }
        finally
        {
            _loginLock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _shutdown.Cancel();
        if (_client.IsConnected) _client.Disconnect();
        _shutdown.Dispose();
        _loginLock.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Takes the login lock, or fails at once rather than waiting.
    ///     A login holds this lock across the Steam Guard prompt, which sits on the user for as long as they
    ///     take. A second caller queueing behind that would be indistinguishable from a hang, so it is told
    ///     what is actually happening instead.
    /// </summary>
    private async Task EnterLoginAsync(CancellationToken token)
    {
        if (!await _loginLock.WaitAsync(TimeSpan.Zero, token).ConfigureAwait(false))
            throw new SteamLoginInProgressException(
                "A Steam login is already under way and may be waiting on you. Finish or cancel it first.");
    }

    /// <summary>
    ///     Drops the connection and forgets any half-finished handshake.
    ///     Called on every failed or cancelled login: without it the socket and the callback pump stay up
    ///     until the process ends, so a Ctrl-C during a login leaves a live Steam connection nobody asked for.
    /// </summary>
    private void Teardown()
    {
        _connected = null;
        _loggedOn = null;
        IsLoggedIn = false;
        CellId = null;
        Licenses = Array.Empty<SteamApps.LicenseListCallback.License>();

        // A fresh signal, so the next login waits for its own licence list rather than reading the one the
        // previous account left behind.
        _licensesReceived = NewLicenseSignal();

        if (_client.IsConnected) _client.Disconnect();
    }

    private static TaskCompletionSource<bool> NewLicenseSignal()
    {
        return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>
    ///     Turns SteamKit's authentication failure into something a host can report, without letting a
    ///     SteamKit type out of this project.
    /// </summary>
    private static Exception Translate(AuthenticationException ex, bool fromCredentials)
    {
        // On a credentials flow this really is the password, because that is the only thing that was sent.
        // The same result on a token logon is handled in LogOnAsync, where it means the opposite.
        if (fromCredentials && ex.Result == EResult.InvalidPassword)
            return new SteamCredentialsRejectedException(
                "Steam did not accept that account name and password", ex);

        if (SteamResults.IsDeadCredential(ex.Result))
            return new SteamLoginRequiredException($"Steam refused the login ({ex.Result}), start again");

        return new SteamException("Steam refused the login", ex.Result, EResult.Invalid, ex);
    }

    /// <summary>
    ///     Deletes the saved token, and says so plainly when it cannot: the provider falls back to an
    ///     environment variable that nothing here can unset, and silently failing to forget a credential is
    ///     worse than admitting it.
    /// </summary>
    private async ValueTask ForgetStoredTokenAsync()
    {
        await _tokenProvider.Delete().ConfigureAwait(false);

        if (_tokenProvider.HaveToken())
            _logger.LogWarning(
                "The Steam login is supplied by the environment, so it cannot be removed from here. Unset it, or it will keep being tried");
    }

    private static uint GenerateLoginId()
    {
        // Drawn fresh per process, so two Wabbajack sessions do not collide with each other either.
        Span<byte> bytes = stackalloc byte[4];
        RandomNumberGenerator.Fill(bytes);
        return BitConverter.ToUInt32(bytes);
    }

    /// <summary>
    ///     Starts the callback pump, once, the first time a connection is actually attempted.
    ///     Not in the constructor, which is where it used to be. This is a DI singleton and
    ///     <c>PreflightRunner.Create</c> resolves the restorer sitting on top of it for every install, so in a
    ///     process that stays open all day - the app, rather than a CLI run that exits in seconds - building
    ///     it eagerly meant a thread waking four times a second for the life of the app on behalf of a user
    ///     who may never log into Steam at all. Nothing arrives before <see cref="_client" /> connects, so
    ///     there is nothing for it to have missed.
    /// </summary>
    private void EnsurePump()
    {
        if (Interlocked.Exchange(ref _pumping, 1) == 1) return;

        new Thread(PumpCallbacks)
        {
            Name = "Steam client callback runner",
            IsBackground = true
        }.Start();
    }

    private void PumpCallbacks()
    {
        while (!_shutdown.IsCancellationRequested)
            try
            {
                _manager.RunWaitCallbacks(TimeSpan.FromMilliseconds(250));
            }
            catch (Exception ex)
            {
                if (_shutdown.IsCancellationRequested) return;
                _logger.LogError(ex, "Error while pumping Steam callbacks");
            }
    }

    private async Task<SteamLoginState?> TryGetStoredStateAsync()
    {
        // Get() throws rather than returning null when nothing is stored.
        if (!_tokenProvider.HaveToken()) return null;

        try
        {
            return await _tokenProvider.Get().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read the saved Steam login, treating it as absent");
            return null;
        }
    }

    private async Task ConnectAsync(CancellationToken token)
    {
        if (_client.IsConnected) return;

        EnsurePump();

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _connected = tcs;

        _logger.LogInformation("Connecting to Steam");
        _client.Connect();

        try
        {
            await tcs.Task.WaitAsync(ConnectTimeout, token).ConfigureAwait(false);
        }
        finally
        {
            _connected = null;
        }
    }

    private async Task<SteamUser.LoggedOnCallback> LogOnAsync(string accountName, string refreshToken,
        bool fromStoredToken, CancellationToken token)
    {
        var tcs = new TaskCompletionSource<SteamUser.LoggedOnCallback>(TaskCreationOptions.RunContinuationsAsynchronously);
        _loggedOn = tcs;

        _steamUser.LogOn(new SteamUser.LogOnDetails
        {
            Username = accountName,
            AccessToken = refreshToken,
            ShouldRememberPassword = true,
            LoginID = _loginId
        });

        SteamUser.LoggedOnCallback callback;
        try
        {
            callback = await tcs.Task.WaitAsync(LogOnTimeout, token).ConfigureAwait(false);
        }
        finally
        {
            _loggedOn = null;
        }

        if (callback.Result != EResult.OK)
        {
            if (SteamResults.IsDeadCredential(callback.Result))
            {
                _logger.LogInformation(
                    "Steam rejected the saved credential as {Result}; it is expired or revoked, not a bad password",
                    callback.Result);

                if (fromStoredToken) await ForgetStoredTokenAsync().ConfigureAwait(false);

                throw new SteamLoginRequiredException(
                    "The saved Steam login is no longer valid, log in again");
            }

            throw new SteamException("Unable to log into Steam", callback.Result, callback.ExtendedResult);
        }

        AccountName = accountName;
        CellId = callback.CellID;
        IsLoggedIn = true;
        _logger.LogInformation("Logged into Steam as {AccountName}", accountName);
        return callback;
    }

    private async Task<SteamLoginResult> CompleteLoginAsync(AuthPollResult poll, CancellationToken token)
    {
        // Log on before storing. A token written first and then rejected by the logon sits on disk
        // unverified, and the dead-credential path will not clear it: that only fires for a login that came
        // from storage, which this one did not.
        var callback = await LogOnAsync(poll.AccountName, poll.RefreshToken, false, token).ConfigureAwait(false);
        var expiresAt = await StoreAsync(poll).ConfigureAwait(false);
        return new SteamLoginResult(poll.AccountName, callback.ClientSteamID, false, expiresAt);
    }

    /// <summary>Returns the expiry that was stored, so a caller can tell the user when this runs out.</summary>
    private async ValueTask<DateTimeOffset?> StoreAsync(AuthPollResult poll)
    {
        var expiresAt = SteamRefreshToken.GetExpiry(poll.RefreshToken);

        await _tokenProvider.SetToken(new SteamLoginState
        {
            AccountName = poll.AccountName,
            RefreshToken = poll.RefreshToken,

            // A null here means Steam has no guard data for this account any more, so drop whatever was
            // stored rather than replaying a stale value.
            GuardData = poll.NewGuardData,

            RefreshTokenExpiresAt = expiresAt

            // poll.AccessToken is deliberately not stored: it is short lived and a fresh one can always be
            // minted from the refresh token.
        }).ConfigureAwait(false);

        return expiresAt;
    }

    private void OnConnected(SteamClient.ConnectedCallback callback)
    {
        _logger.LogInformation("Connected to Steam");
        _connected?.TrySetResult(true);
    }

    private void OnDisconnected(SteamClient.DisconnectedCallback callback)
    {
        IsLoggedIn = false;
        _logger.LogInformation("Disconnected from Steam");
        _connected?.TrySetException(new SteamException("Disconnected from Steam before connecting",
            EResult.NoConnection, EResult.Invalid));
        _loggedOn?.TrySetException(new SteamException("Disconnected from Steam while logging in",
            EResult.NoConnection, EResult.Invalid));

        // Nothing is going to arrive over a dropped connection, so release anyone waiting on the licence
        // list now rather than leaving them on a timeout.
        _licensesReceived.TrySetResult(false);
    }

    private void OnLoggedOn(SteamUser.LoggedOnCallback callback)
    {
        _loggedOn?.TrySetResult(callback);
    }

    private void OnLoggedOff(SteamUser.LoggedOffCallback callback)
    {
        IsLoggedIn = false;
        _logger.LogInformation("Logged off Steam ({Result})", callback.Result);
    }

    private void OnLicenseList(SteamApps.LicenseListCallback callback)
    {
        if (callback.Result != EResult.OK)
        {
            _logger.LogWarning("Steam did not return the account's licences ({Result})", callback.Result);

            // Still release anything waiting. A failed licence list is an entitlement answer of "nothing
            // known", which the free-content fallback can still get past; blocking forever is not.
            _licensesReceived.TrySetResult(false);
            return;
        }

        Licenses = callback.LicenseList.ToArray();
        _logger.LogInformation("Steam returned {Count} licences", Licenses.Count);
        _licensesReceived.TrySetResult(true);
    }
}
