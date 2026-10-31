using System;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Wabbajack.App.Avalonia.ViewModels;
using Wabbajack.Downloaders;
using Wabbajack.DTOs.Logins;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.Networking.NexusApi;

namespace Wabbajack.App.Avalonia.LoginManagers;

/// <summary>
///     The Nexus Mods tile. Ported from the WPF app unchanged in what it decides; see that class's history
///     for why each rule is the way it is. In short: logged in means <see cref="NexusCredential.CanDownload" />,
///     the one definition the downloader and preflight share; logging in has to stay possible while logged
///     in, because an expired login still reads as logged in; one login at a time.
/// </summary>
public partial class NexusLoginManager : ViewModel, INeedsLogin
{
    private readonly ILogger<NexusLoginManager> _logger;
    private readonly ITokenProvider<NexusOAuthState> _token;
    private readonly NexusApi _api;
    private readonly IServiceProvider _serviceProvider;
    private readonly Subject<Unit> _refreshed = new();

    /// <summary>1 while a login is in flight. Set when one starts, cleared when it ends either way.</summary>
    private int _loginRunning;

    public NexusLoginManager(ILogger<NexusLoginManager> logger, ITokenProvider<NexusOAuthState> token, NexusApi api,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _token = token;
        _api = api;
        _serviceProvider = serviceProvider;
        Task.Run(RefreshTokenState);

        var clearLogin = ReactiveCommand.CreateFromTask(async () =>
        {
            _logger.LogInformation("Deleting Login information for {SiteName}", SiteName);
            await ClearLoginToken();
        }, this.WhenAnyValue(v => v.LoggedIn));
        ClearLogin = clearLogin;

        Icon = Application.Current?.FindResource("NexusLogo") as IImage;

        // No canExecute: an expired login still reads as logged in, and logging in again is the fix.
        var triggerLogin = ReactiveCommand.Create(() =>
        {
            _logger.LogInformation("Logging into {SiteName}", SiteName);
            StartLogin();
        });
        TriggerLogin = triggerLogin;

        // The tile's only button. When logging out cannot change anything (the login comes from the
        // environment), it falls through to logging in, which is the one action left that does.
        var toggleLogin = ReactiveCommand.CreateFromTask(async () =>
        {
            if (LoggedIn && await ClearLoginToken()) return;
            StartLogin();
        });
        ToggleLogin = toggleLogin;

        // An unobserved ReactiveCommand exception is rethrown on the UI thread and takes the app with it.
        foreach (var thrown in new[] { clearLogin.ThrownExceptions, triggerLogin.ThrownExceptions, toggleLogin.ThrownExceptions })
            thrown.Subscribe(ex => _logger.LogError(ex, "A Nexus Mods login command failed"))
                .DisposeWith(CompositeDisposable);
    }

    public string SiteName { get; } = "Nexus Mods";
    public ICommand TriggerLogin { get; }
    public ICommand ClearLogin { get; }
    public ICommand ToggleLogin { get; }
    public IImage? Icon { get; }

    [Reactive] public partial bool LoggedIn { get; set; }

    /// <summary>
    ///     Fires every time the stored token has been re-read, whether or not that changed
    ///     <see cref="LoggedIn" />. Nothing is replayed.
    /// </summary>
    public IObservable<Unit> Refreshed => _refreshed;

    public Type LoginFor() => typeof(NexusDownloader);

    /// <summary>Deletes the stored login and re-reads what is left. False when there was nothing stored to delete.</summary>
    private async Task<bool> ClearLoginToken()
    {
        var deleted = await _token.Delete();
        await RefreshTokenState();
        if (!deleted)
            _logger.LogInformation(
                "No stored {SiteName} login to delete; any credential left comes from this machine's environment",
                SiteName);
        return deleted;
    }

    /// <summary>
    ///     Sends the user to Nexus Mods in their browser and waits for the redirect, unless a login is already
    ///     in flight. Returns at once; the token state is re-read however the login ends.
    /// </summary>
    private void StartLogin()
    {
        if (Interlocked.Exchange(ref _loginRunning, 1) == 1)
        {
            _logger.LogInformation("A {SiteName} login is already in progress", SiteName);
            return;
        }

        var handler = _serviceProvider.GetRequiredService<NexusLoginHandler>();
        Task.Run(async () =>
        {
            try
            {
                await handler.LogIn(CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "The {SiteName} login failed", SiteName);
            }
            finally
            {
                Interlocked.Exchange(ref _loginRunning, 0);
                await RefreshTokenState();
            }
        });
    }

    /// <summary>
    ///     Re-reads the credential. Both the new state and <see cref="Refreshed" /> are delivered on the UI
    ///     thread and in that order, because the tile is bound to <see cref="LoggedIn" /> and Avalonia, unlike
    ///     WPF, refuses a change raised from any other thread.
    /// </summary>
    private async Task RefreshTokenState()
    {
        bool loggedIn;
        try
        {
            loggedIn = (await _api.CredentialSource()).CanDownload();
        }
        catch (Exception ex)
        {
            // Reading the credential can refresh the token, so this covers a network failure as well as an
            // unreadable store. Nothing usable was established either way.
            _logger.LogError(ex, "Failed to refresh Nexus token state");
            loggedIn = false;
        }

        RxApp.MainThreadScheduler.Schedule(() =>
        {
            LoggedIn = loggedIn;
            _refreshed.OnNext(Unit.Default);
        });
    }
}
