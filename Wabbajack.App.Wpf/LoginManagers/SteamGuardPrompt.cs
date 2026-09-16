using System;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Wabbajack.Networking.Steam;

namespace Wabbajack.LoginManagers;

/// <summary>Which of Steam Guard's three questions is being asked.</summary>
public enum SteamGuardKind
{
    /// <summary>A code from the Steam mobile authenticator.</summary>
    DeviceCode,

    /// <summary>A code Steam emailed to the account's address.</summary>
    EmailCode,

    /// <summary>Nothing to type: the account approves the sign-in in the Steam mobile app.</summary>
    DeviceConfirmation
}

/// <summary>
///     One question Steam Guard asked, and the answer the user has not given yet. Held by
///     <see cref="SteamGuardPrompt" /> while it waits, and bound by the login pane.
/// </summary>
public partial class SteamGuardRequest : ReactiveObject
{
    private readonly TaskCompletionSource<string?> _answer =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public SteamGuardRequest(SteamGuardKind kind, string? email, bool previousCodeWasIncorrect)
    {
        Kind = kind;
        Email = email;
        PreviousCodeWasIncorrect = previousCodeWasIncorrect;
        Code = string.Empty;

        SubmitCommand = ReactiveCommand.Create(() => { _answer.TrySetResult(Code.Trim()); },
            this.WhenAnyValue(x => x.Code, code => !string.IsNullOrWhiteSpace(code)));
    }

    public SteamGuardKind Kind { get; }

    /// <summary>The address Steam sent the code to, for <see cref="SteamGuardKind.EmailCode" />.</summary>
    public string? Email { get; }

    /// <summary>Set when Steam rejected the last code, so the pane can say so rather than just asking again.</summary>
    public bool PreviousCodeWasIncorrect { get; }

    /// <summary>False for a device confirmation, which is answered on the phone and not here.</summary>
    public bool NeedsCode => Kind != SteamGuardKind.DeviceConfirmation;

    public string Title => Kind switch
    {
        SteamGuardKind.DeviceCode => "Steam Guard code",
        SteamGuardKind.EmailCode => "Check your email",
        _ => "Approve this sign-in"
    };

    public string Message => Kind switch
    {
        SteamGuardKind.DeviceCode =>
            "Open the Steam mobile app and type the code it is showing.",
        SteamGuardKind.EmailCode => string.IsNullOrWhiteSpace(Email)
            ? "Steam emailed you a code. Type it here."
            : $"Steam emailed a code to {Email}. Type it here.",
        _ => "Steam has asked your phone to confirm this sign-in. Approve \"Wabbajack\" there and this " +
             "carries on by itself."
    };

    [Reactive] public partial string Code { get; set; }

    public ReactiveCommand<Unit, Unit> SubmitCommand { get; }

    /// <summary>The code, or null once the user has given up. Never completes on its own.</summary>
    public Task<string?> Answer => _answer.Task;

    /// <summary>
    ///     Gives up on this question. Null is how <see cref="ISteamGuardPrompt" /> says the user walked away:
    ///     SteamKit's own retry loop is infinite and throws on a null code, so this is the only way out of a
    ///     login that is sitting on a human.
    /// </summary>
    public void Cancel()
    {
        _answer.TrySetResult(null);
    }
}

/// <summary>
///     The app's <see cref="ISteamGuardPrompt" />: publishes whatever Steam Guard is asking as
///     <see cref="Pending" /> and waits for the login pane to answer it.
///     <para>
///         Registered ahead of <c>AddSteam</c>, whose own default raises an intervention that nothing in this
///         app handles - <c>IUserInterventionHandler</c> here is the throwing one - and which would take a
///         login down mid-flow.
///     </para>
///     <para>
///         Every method is called from inside SteamKit's polling loop, on its thread. Nothing here blocks
///         that thread: the two code questions hand back a task that completes when the user answers, and
///         <see cref="Pending" /> is only ever written on the UI thread because a view is bound to it.
///     </para>
/// </summary>
public partial class SteamGuardPrompt : ReactiveObject, ISteamGuardPrompt
{
    /// <summary>What Steam Guard is waiting on right now, or null when it is waiting on nothing.</summary>
    [Reactive] public partial SteamGuardRequest? Pending { get; set; }

    public Task<string?> GetDeviceCodeAsync(bool previousCodeWasIncorrect, CancellationToken token)
    {
        return Ask(new SteamGuardRequest(SteamGuardKind.DeviceCode, null, previousCodeWasIncorrect), token);
    }

    public Task<string?> GetEmailCodeAsync(string email, bool previousCodeWasIncorrect, CancellationToken token)
    {
        return Ask(new SteamGuardRequest(SteamGuardKind.EmailCode, email, previousCodeWasIncorrect), token);
    }

    /// <summary>
    ///     Always waits for the mobile app, as every implementation does; declining would send the user to a
    ///     typed code instead. The request is published anyway, with nothing to type, so the pane can say
    ///     what the user's phone is about to ask them rather than appearing to have stalled.
    /// </summary>
    public Task<bool> AcceptDeviceConfirmationAsync(CancellationToken token)
    {
        Show(new SteamGuardRequest(SteamGuardKind.DeviceConfirmation, null, false));
        return Task.FromResult(true);
    }

    /// <summary>
    ///     Drops whatever is on screen. Called by the login pane when an attempt ends: a device confirmation
    ///     is never answered here, so it is the end of the login that retires it.
    /// </summary>
    public void Reset()
    {
        Show(null);
    }

    private async Task<string?> Ask(SteamGuardRequest request, CancellationToken token)
    {
        Show(request);
        await using var registration = token.Register(request.Cancel);

        try
        {
            return await request.Answer;
        }
        finally
        {
            Clear(request);
        }
    }

    private void Show(SteamGuardRequest? request)
    {
        RxApp.MainThreadScheduler.Schedule(() => Pending = request);
    }

    /// <summary>
    ///     Only clears the question it was asked about. A rejected code is followed at once by the next
    ///     question, and the two calls can land in either order.
    /// </summary>
    private void Clear(SteamGuardRequest request)
    {
        RxApp.MainThreadScheduler.Schedule(() =>
        {
            if (ReferenceEquals(Pending, request)) Pending = null;
        });
    }
}
