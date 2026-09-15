using System;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Wabbajack.Common;
using Wabbajack.LoginManagers;
using Wabbajack.Networking.Steam;

namespace Wabbajack;

public enum SteamLoginMode
{
    /// <summary>Scan a code with the Steam mobile app. No password is handled at all.</summary>
    QrCode,

    /// <summary>Account name and password, for an account without the mobile authenticator.</summary>
    Credentials
}

/// <summary>
///     The Steam login pane. Shown as a floating pane over whatever the user was doing, by preflight when a
///     modlist needs game files it can fetch, and by the Logins settings tile.
///     <para>
///         QR is the default because it is the only path that never handles a password: the phone approves
///         the sign-in and Steam hands back a token. It needs the Steam mobile app, though, and its only
///         confirmation type is that approval - there is no typed-code fallback - so the account name and
///         password path is offered beside it rather than buried.
///     </para>
///     <para>
///         One attempt at a time. <c>SteamSession</c> holds its login lock for the whole of a flow, including
///         however long the user takes over a Steam Guard code, and refuses a second caller rather than
///         queueing it - so switching between the two paths cancels the attempt in flight and waits for it to
///         unwind before starting the next.
///     </para>
/// </summary>
public partial class SteamLoginVM : ViewModel, IClosableVM
{
    private readonly CancellationTokenSource _closed = new();
    private readonly ILogger<SteamLoginVM> _logger;

    /// <summary>Serialises <see cref="Restart" />, which is otherwise re-entered by a second click.</summary>
    private readonly SemaphoreSlim _restarting = new(1, 1);

    private readonly TaskCompletionSource<bool> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ISteamSession _session;

    private CancellationTokenSource? _attempt;

    /// <summary>
    ///     Set before anything is torn down, and checked before <see cref="_restarting" /> is taken, so no
    ///     caller can reach a disposed semaphore or a disposed <see cref="_closed" />.
    /// </summary>
    private volatile bool _disposed;

    private Task _running = Task.CompletedTask;

    public SteamLoginVM(ILogger<SteamLoginVM> logger, ISteamSession session, SteamGuardPrompt guard)
    {
        _logger = logger;
        _session = session;
        Guard = guard;

        Username = string.Empty;
        StatusText = string.Empty;

        UseQrCommand = ReactiveCommand.CreateFromTask(() => Restart(SteamLoginMode.QrCode, null, null));
        UseCredentialsCommand = ReactiveCommand.CreateFromTask(() => Stop(SteamLoginMode.Credentials));

        SignInCommand = ReactiveCommand.CreateFromTask<string>(
            password => Restart(SteamLoginMode.Credentials, Username.Trim(), password),
            this.WhenAnyValue(x => x.Username, x => x.HasPassword, x => x.IsBusy,
                (user, password, busy) => !busy && password && !string.IsNullOrWhiteSpace(user)));

        var close = ReactiveCommand.Create(Close);
        CloseCommand = close;

        foreach (var thrown in new[]
                 {
                     UseQrCommand.ThrownExceptions, UseCredentialsCommand.ThrownExceptions,
                     SignInCommand.ThrownExceptions, close.ThrownExceptions
                 })
            thrown.Subscribe(ex => _logger.LogError(ex, "A Steam login command failed"))
                .DisposeWith(CompositeDisposable);
    }

    /// <summary>Whatever Steam Guard is asking mid-flow, for the pane to draw. Shared, and a singleton.</summary>
    public SteamGuardPrompt Guard { get; }

    [Reactive] public partial SteamLoginMode Mode { get; set; }

    /// <summary>The URL the QR code encodes. Steam rotates it every few seconds while it polls.</summary>
    [Reactive] public partial string? ChallengeUrl { get; set; }

    [Reactive] public partial string StatusText { get; set; }

    /// <summary>Why the last attempt did not work, in words for the user. Null while nothing has gone wrong.</summary>
    [Reactive] public partial string? ErrorText { get; set; }

    [Reactive] public partial bool IsBusy { get; set; }

    [Reactive] public partial string Username { get; set; }

    /// <summary>
    ///     Whether the password box has anything in it. The password itself never reaches this view model:
    ///     the view hands it straight to <see cref="SignInCommand" />, which hands it straight to Steam.
    /// </summary>
    [Reactive] public partial bool HasPassword { get; set; }

    public ReactiveCommand<Unit, Unit> UseQrCommand { get; }
    public ReactiveCommand<Unit, Unit> UseCredentialsCommand { get; }
    public ReactiveCommand<string, Unit> SignInCommand { get; }

    /// <summary>True once the account is logged in, false once the user gave up. Never faults.</summary>
    public Task<bool> Result => _result.Task;

    public ICommand CloseCommand { get; }

    /// <summary>Opens on the QR code, which is the path that asks the user for the least.</summary>
    public void Start()
    {
        Restart(SteamLoginMode.QrCode, null, null).FireAndForget();
    }

    /// <summary>
    ///     Stops whatever is in flight, waits for it to let go of Steam's login lock, and starts the given
    ///     path. A null <paramref name="username" /> means the QR path, which asks for nothing.
    /// </summary>
    private async Task Restart(SteamLoginMode mode, string? username, string? password)
    {
        if (_disposed) return;

        await _restarting.WaitAsync();
        try
        {
            await StopCore(mode);
            if (_closed.IsCancellationRequested || _result.Task.IsCompleted) return;

            var attempt = CancellationTokenSource.CreateLinkedTokenSource(_closed.Token);
            _attempt = attempt;
            _running = Run(mode, username, password, attempt.Token);
        }
        finally
        {
            _restarting.Release();
        }
    }

    /// <summary>Cancels the attempt in flight and switches the pane to <paramref name="mode" />.</summary>
    private async Task Stop(SteamLoginMode mode)
    {
        if (_disposed) return;

        await _restarting.WaitAsync();
        try
        {
            await StopCore(mode);
        }
        finally
        {
            _restarting.Release();
        }
    }

    private async Task StopCore(SteamLoginMode mode)
    {
        await Unwind();

        Mode = mode;
        ChallengeUrl = null;
        ErrorText = null;
        StatusText = string.Empty;
        IsBusy = false;
        Guard.Reset();
    }

    /// <summary>
    ///     Cancels the attempt in flight, waits for it to let go of Steam's login lock, and disposes it.
    ///     Each attempt links its own source to <see cref="_closed" />, which is a registration on that
    ///     source; left undisposed, every switch between the code and the password would leave one behind.
    ///     Callers hold <see cref="_restarting" />, so nothing else is touching <see cref="_attempt" />.
    /// </summary>
    private async Task Unwind()
    {
        _attempt?.Cancel();

        try
        {
            await _running;
        }
        catch (Exception)
        {
            // Run swallows its own failures; anything here is the cancellation that was just asked for.
        }

        _attempt?.Dispose();
        _attempt = null;
        _running = Task.CompletedTask;
    }

    private async Task Run(SteamLoginMode mode, string? username, string? password, CancellationToken token)
    {
        IsBusy = true;
        StatusText = mode == SteamLoginMode.QrCode
            ? "Asking Steam for a code"
            : $"Signing in as {username}";

        try
        {
            var result = mode == SteamLoginMode.QrCode
                ? await _session.LoginWithQrCodeAsync(OnChallengeUrl, token)
                : await _session.LoginWithCredentialsAsync(username!, password!, token);

            _logger.LogInformation("Logged into Steam as {AccountName}", result.AccountName);
            StatusText = $"Logged in as {result.AccountName}";
            _result.TrySetResult(true);
        }
        catch (OperationCanceledException)
        {
            // The user switched paths or closed the pane; StopCore has already tidied the fields.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Steam login failed");
            ErrorText = Describe(ex);
            StatusText = string.Empty;
            ChallengeUrl = null;
        }
        finally
        {
            IsBusy = false;
            Guard.Reset();
        }
    }

    /// <summary>
    ///     Steam rotates the challenge URL as a side effect of polling, so every call after the first arrives
    ///     on SteamKit's polling thread. The pane redraws the code from it, so it is marshalled here rather
    ///     than trusted to arrive somewhere useful.
    /// </summary>
    private void OnChallengeUrl(string url)
    {
        RxApp.MainThreadScheduler.Schedule(() =>
        {
            ChallengeUrl = url;
            StatusText = "Waiting for you to scan the code";
        });
    }

    /// <summary>
    ///     What went wrong, said to the person in front of it. <see cref="SteamCredentialsRejectedException" />
    ///     is the one place a wrong password is the right thing to say: <c>SteamSession</c> keeps an expired
    ///     or revoked token, which Steam also reports as a bad password, well away from it.
    /// </summary>
    private static string Describe(Exception ex)
    {
        return ex switch
        {
            SteamCredentialsRejectedException => "Steam did not accept that account name and password.",
            SteamLoginInProgressException e => e.Message,
            SteamLoginRequiredException e => e.Message,
            SteamException e => $"Steam refused the login ({e.Result}).",
            TimeoutException => "Steam did not answer in time. Check your connection and try again.",
            _ => ex.Message
        };
    }

    private void Close()
    {
        _closed.Cancel();
        _result.TrySetResult(false);
    }

    /// <summary>
    ///     Closing the pane is what ends the login; the rest of the tidying waits for the attempt to unwind,
    ///     which Dispose cannot do without blocking the UI thread. <see cref="_disposed" /> is set first, so
    ///     anything arriving after this returns before it reaches what <see cref="Release" /> is about to
    ///     dispose.
    /// </summary>
    public override void Dispose()
    {
        if (_disposed) return;

        Close();
        _disposed = true;
        base.Dispose();
        Release().FireAndForget();
    }

    private async Task Release()
    {
        await _restarting.WaitAsync();
        try
        {
            await Unwind();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "While shutting down a Steam login");
        }
        finally
        {
            _restarting.Release();
        }

        _restarting.Dispose();
        _closed.Dispose();
    }
}
