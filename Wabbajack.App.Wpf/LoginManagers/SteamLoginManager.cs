using System;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Wabbajack.Messages;
using Wabbajack.Networking.Steam;

namespace Wabbajack.LoginManagers;

/// <summary>
///     The Steam tile in the Logins settings pane.
///     <para>
///         Steam is listed there with Nexus Mods, but it is not a download source the way Nexus is: no
///         modlist is fetched from it, nothing needs it to be logged in ahead of time, and the app only ever
///         asks when a list turns out to need game files the user's install does not have. What the pane is
///         actually for is the credentials this machine is holding - which is exactly the question "how do I
///         get rid of the Steam login Wabbajack saved" - so the tile is here, and its real job is the logout.
///         Logging in from here is offered too, for someone who would rather get it out of the way, and goes
///         through the same pane preflight uses.
///     </para>
///     <para>
///         <see cref="LoginFor" /> answers with the session rather than a downloader, which deliberately
///         matches none: <c>CompilerMainVM</c> looks a login up by the downloader it belongs to, and Steam
///         belongs to no downloader.
///     </para>
/// </summary>
public partial class SteamLoginManager : ViewModel, INeedsLogin
{
    private readonly ILogger<SteamLoginManager> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ISteamSession _session;

    /// <summary>1 while the login pane is up, so a second click does not queue a second one behind it.</summary>
    private int _paneOpen;

    public SteamLoginManager(ILogger<SteamLoginManager> logger, ISteamSession session,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _session = session;
        _serviceProvider = serviceProvider;

        Refresh();

        var clearLogin = ReactiveCommand.CreateFromTask(async () =>
        {
            _logger.LogInformation("Deleting Login information for {SiteName}", SiteName);
            await Logout();
        }, this.WhenAnyValue(v => v.LoggedIn));
        ClearLogin = clearLogin;

        var triggerLogin = ReactiveCommand.Create(StartLogin);
        TriggerLogin = triggerLogin;

        // One button on the tile, so it has to cover both directions. A login that cannot be deleted is one
        // the environment is supplying, and logging in again is the only thing left that changes the answer,
        // since a stored login shadows the variable.
        var toggleLogin = ReactiveCommand.CreateFromTask(async () =>
        {
            if (LoggedIn && await Logout()) return;
            StartLogin();
        });
        ToggleLogin = toggleLogin;

        // An unobserved ReactiveCommand exception is rethrown on the UI thread and takes the app with it.
        foreach (var thrown in new[]
                     {clearLogin.ThrownExceptions, triggerLogin.ThrownExceptions, toggleLogin.ThrownExceptions})
            thrown.Subscribe(ex => _logger.LogError(ex, "A Steam login command failed"))
                .DisposeWith(CompositeDisposable);
    }

    public string SiteName { get; } = "Steam";
    public ICommand TriggerLogin { get; set; }
    public ICommand ClearLogin { get; set; }
    public ICommand ToggleLogin { get; set; }

    /// <summary>
    ///     No icon: Steam's logo is not one of this app's resources, and the tile draws its own glyph rather
    ///     than an approximation of somebody else's mark.
    /// </summary>
    public ImageSource Icon { get; set; }

    [Reactive] public partial bool LoggedIn { get; set; }

    public Type LoginFor()
    {
        return typeof(ISteamSession);
    }

    /// <summary>
    ///     A saved token counts as logged in. It may turn out to be expired or revoked - only a login can say
    ///     - but it is a credential this machine is holding, which is what the tile is reporting on.
    /// </summary>
    private void Refresh()
    {
        LoggedIn = _session.IsLoggedIn || _session.HaveStoredToken;
    }

    /// <summary>
    ///     Deletes the saved login. False when there was nothing to delete, which is not the same as being
    ///     logged out: a credential from the environment is still there afterwards and cannot be removed
    ///     from here.
    /// </summary>
    private async Task<bool> Logout()
    {
        var result = await _session.LogoutAsync();
        Refresh();

        switch (result)
        {
            case SteamLogoutResult.Deleted:
                _logger.LogInformation("Logged out of Steam and deleted the saved login");
                return true;

            case SteamLogoutResult.HeldInEnvironment:
                _logger.LogWarning(
                    "Logged out of Steam, but the login comes from this machine's environment and is still in place. Unset it to finish logging out");
                return false;

            default:
                _logger.LogInformation("There was no saved {SiteName} login to delete", SiteName);
                return false;
        }
    }

    private void StartLogin()
    {
        if (Interlocked.Exchange(ref _paneOpen, 1) == 1)
        {
            _logger.LogInformation("A {SiteName} login is already open", SiteName);
            return;
        }

        var pane = _serviceProvider.GetRequiredService<SteamLoginVM>();
        ShowSteamLogin.Send(pane).ContinueWith(_ =>
        {
            Interlocked.Exchange(ref _paneOpen, 0);
            RxApp.MainThreadScheduler.Schedule(() =>
            {
                Refresh();
                pane.Dispose();
            });
        });
    }
}
