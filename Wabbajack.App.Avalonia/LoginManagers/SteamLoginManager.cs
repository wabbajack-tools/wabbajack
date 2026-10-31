using System;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Media;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Wabbajack.App.Avalonia.Messages;
using Wabbajack.App.Avalonia.ViewModels;
using Wabbajack.Networking.Steam;

namespace Wabbajack.App.Avalonia.LoginManagers;

/// <summary>
///     The Steam tile. Steam is not a download source; the tile is here because it is where a user looks to
///     get rid of the login Wabbajack saved, and logging in from here goes through the same pane preflight
///     uses. <see cref="LoginFor" /> deliberately matches no downloader.
/// </summary>
public partial class SteamLoginManager : ViewModel, INeedsLogin
{
    private readonly ILogger<SteamLoginManager> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ISteamSession _session;

    /// <summary>1 while the login pane is up, so a second click does not queue a second one behind it.</summary>
    private int _paneOpen;

    public SteamLoginManager(ILogger<SteamLoginManager> logger, ISteamSession session, IServiceProvider serviceProvider)
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

        var toggleLogin = ReactiveCommand.CreateFromTask(async () =>
        {
            if (LoggedIn && await Logout()) return;
            StartLogin();
        });
        ToggleLogin = toggleLogin;

        foreach (var thrown in new[] { clearLogin.ThrownExceptions, triggerLogin.ThrownExceptions, toggleLogin.ThrownExceptions })
            thrown.Subscribe(ex => _logger.LogError(ex, "A Steam login command failed"))
                .DisposeWith(CompositeDisposable);
    }

    public string SiteName { get; } = "Steam";
    public ICommand TriggerLogin { get; }
    public ICommand ClearLogin { get; }
    public ICommand ToggleLogin { get; }

    /// <summary>No icon: Steam's logo is not one of this app's resources, and the tile draws a glyph instead.</summary>
    public IImage? Icon => null;

    [Reactive] public partial bool LoggedIn { get; set; }

    public Type LoginFor() => typeof(ISteamSession);

    /// <summary>A saved token counts as logged in: it is a credential this machine is holding.</summary>
    private void Refresh() => LoggedIn = _session.IsLoggedIn || _session.HaveStoredToken;

    /// <summary>Deletes the saved login. False when there was nothing to delete.</summary>
    private async Task<bool> Logout()
    {
        var result = await _session.LogoutAsync();
        RxApp.MainThreadScheduler.Schedule(Refresh);

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
