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
    ///     Steam identifies a session by the pair (public IP, LoginID), and refuses two sessions on one account
    ///     sharing both. SteamKit's default LoginID is derived from the machine's primary bind address, which is
    ///     exactly the value the user's own Steam client is already using -- logging in with it kicks them out
    ///     of Steam. That reads as "Wabbajack broke my Steam", so this is a correctness requirement, not a
    ///     nicety. Any value that is ours alone will do.
    /// </summary>
    private readonly uint _loginId = GenerateLoginId();

    private readonly SemaphoreSlim _loginLock = new(1, 1);
    private readonly CallbackManager _manager;
    private readonly ISteamGuardPrompt _prompt;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly SteamApps _steamApps;
    private readonly SteamUser _steamUser;
    private readonly ITokenProvider<SteamLoginState> _tokenProvider;

    private TaskCompletionSource<bool>? _connected;
    private bool _disposed;
    private TaskCompletionSource<SteamUser.LoggedOnCallback>? _loggedOn;

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

        _manager.Subscribe<SteamClient.ConnectedCallback>(OnConnected);
        _manager.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected);
        _manager.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);
        _manager.Subscribe<SteamUser.LoggedOffCallback>(OnLoggedOff);
        _manager.Subscribe<SteamApps.LicenseListCallback>(OnLicenseList);

        new Thread(PumpCallbacks)
        {
            Name = "Steam client callback runner",
            IsBackground = true
        }.Start();
    }

    /// <summary>
    ///     The licences the logged in account holds. Empty until Steam sends them, shortly after logon.
    /// </summary>
    public IReadOnlyList<SteamApps.LicenseListCallback.License> Licenses { get; private set; } =
        Array.Empty<SteamApps.LicenseListCallback.License>();

    public SteamApps Apps => _steamApps;

    public SteamConfiguration Configuration => _client.Configuration;

    public bool IsLoggedIn { get; private set; }

    public string? AccountName { get; private set; }

    public bool HaveStoredToken => _tokenProvider.HaveToken();

    public async Task<SteamLoginResult> LoginWithStoredTokenAsync(CancellationToken token)
    {
        await _loginLock.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var state = await TryGetStoredStateAsync().ConfigureAwait(false);
            if (state == null || string.IsNullOrWhiteSpace(state.RefreshToken))
                throw new SteamLoginRequiredException("There is no saved Steam login");

            if (SteamRefreshToken.IsExpired(state.RefreshTokenExpiresAt, DateTimeOffset.UtcNow))
            {
                await _tokenProvider.Delete().ConfigureAwait(false);
                throw new SteamLoginRequiredException("The saved Steam login has expired, log in again");
            }

            await ConnectAsync(token).ConfigureAwait(false);
            var callback = await LogOnAsync(state.AccountName, state.RefreshToken, true, token).ConfigureAwait(false);
            return new SteamLoginResult(state.AccountName, callback.ClientSteamID, true);
        }
        finally
        {
            _loginLock.Release();
        }
    }

    public async Task<SteamLoginResult> LoginWithQrCodeAsync(Action<string> onChallengeUrl, CancellationToken token)
    {
        await _loginLock.WaitAsync(token).ConfigureAwait(false);
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
        finally
        {
            _loginLock.Release();
        }
    }

    public async Task<SteamLoginResult> LoginWithCredentialsAsync(string username, string password,
        CancellationToken token)
    {
        await _loginLock.WaitAsync(token).ConfigureAwait(false);
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
        finally
        {
            _loginLock.Release();
        }
    }

    public async ValueTask<bool> LogoutAsync()
    {
        if (_client.IsConnected)
            _steamUser.LogOff();

        IsLoggedIn = false;
        AccountName = null;
        Licenses = Array.Empty<SteamApps.LicenseListCallback.License>();

        return await _tokenProvider.Delete().ConfigureAwait(false);
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

    private static uint GenerateLoginId()
    {
        Span<byte> bytes = stackalloc byte[4];
        RandomNumberGenerator.Fill(bytes);
        // The top bit keeps us well clear of the small, bind-address-derived ids Steam's own clients use.
        return BitConverter.ToUInt32(bytes) | 0x8000_0000u;
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

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _connected = tcs;

        _logger.LogInformation("Connecting to Steam");
        _client.Connect();

        await tcs.Task.WaitAsync(ConnectTimeout, token).ConfigureAwait(false);
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

        var callback = await tcs.Task.WaitAsync(LogOnTimeout, token).ConfigureAwait(false);

        if (callback.Result != EResult.OK)
        {
            if (SteamResults.IsDeadCredential(callback.Result))
            {
                _logger.LogInformation(
                    "Steam rejected the saved credential as {Result}; it is expired or revoked, not a bad password",
                    callback.Result);

                if (fromStoredToken) await _tokenProvider.Delete().ConfigureAwait(false);

                throw new SteamLoginRequiredException(
                    "The saved Steam login is no longer valid, log in again");
            }

            throw new SteamException("Unable to log into Steam", callback.Result, callback.ExtendedResult);
        }

        AccountName = accountName;
        IsLoggedIn = true;
        _logger.LogInformation("Logged into Steam as {AccountName}", accountName);
        return callback;
    }

    private async Task<SteamLoginResult> CompleteLoginAsync(AuthPollResult poll, CancellationToken token)
    {
        await StoreAsync(poll).ConfigureAwait(false);
        var callback = await LogOnAsync(poll.AccountName, poll.RefreshToken, false, token).ConfigureAwait(false);
        return new SteamLoginResult(poll.AccountName, callback.ClientSteamID, false);
    }

    private async ValueTask StoreAsync(AuthPollResult poll)
    {
        await _tokenProvider.SetToken(new SteamLoginState
        {
            AccountName = poll.AccountName,
            RefreshToken = poll.RefreshToken,

            // A null here means Steam has no guard data for this account any more, so drop whatever was
            // stored rather than replaying a stale value.
            GuardData = poll.NewGuardData,

            RefreshTokenExpiresAt = SteamRefreshToken.GetExpiry(poll.RefreshToken)

            // poll.AccessToken is deliberately not stored: it is short lived and a fresh one can always be
            // minted from the refresh token.
        }).ConfigureAwait(false);
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
            return;
        }

        Licenses = callback.LicenseList.ToArray();
        _logger.LogInformation("Steam returned {Count} licences", Licenses.Count);
    }
}
