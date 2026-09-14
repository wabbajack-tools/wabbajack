using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Wabbajack.Downloaders;
using Wabbajack.DTOs.Logins;
using Wabbajack.Messages;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.Networking.NexusApi;
using Wabbajack.UserIntervention;

namespace Wabbajack.LoginManagers;

public partial class NexusLoginManager : ViewModel, ILoginFor<NexusDownloader>
{
    private readonly ILogger<NexusLoginManager> _logger;
    private readonly ITokenProvider<NexusOAuthState> _token;
    private readonly NexusApi _api;
    private readonly IServiceProvider _serviceProvider;
    private readonly Subject<Unit> _refreshed = new();

    /// <summary>1 while a login window is open. Set here, cleared by that window's Closed event.</summary>
    private int _loginWindowOpen;

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

    /// <summary>
    ///     Fires every time the stored token has been re-read, whether or not that changed
    ///     <see cref="LoggedIn" />. A token this app still considers valid can have been revoked at the other
    ///     end, so a login that leaves <see cref="LoggedIn" /> true is still news to anyone asking the API.
    ///     Nothing is replayed, so a subscriber never sees the state it started with.
    /// </summary>
    public IObservable<Unit> Refreshed => _refreshed;

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

        Icon = (DrawingImage)Application.Current.Resources["NexusLogo"];

        // No canExecute: logging in again has to be possible while already logged in, because that is
        // exactly the state preflight asks about when it says "your Nexus Mods login has expired". LoggedIn
        // is now NexusCredential.CanDownload over the stored credential, which a revoked API key or a token
        // Nexus has stopped honouring still satisfies - so gating this on !LoggedIn made the one button
        // offered for the one thing to do silently refuse, and pushed that refusal into ThrownExceptions,
        // which nothing in this app observes.
        var triggerLogin = ReactiveCommand.Create(() =>
        {
            _logger.LogInformation("Logging into {SiteName}", SiteName);
            StartLogin();
        });
        TriggerLogin = triggerLogin;

        // Calls the work directly rather than executing the commands above, so the settings tile cannot
        // execute one that refuses either: LoggedIn can change between the check and the execution.
        //
        // It is the tile's only button, so it also has to have somewhere to go when logging out cannot
        // change anything. LoggedIn means a usable credential is in reach, not that there is a stored login
        // to delete: a host can hand this app one through NEXUS_OAUTH_INFO, and Delete then removes nothing
        // while LoggedIn stays true - a button that says "Log out", does nothing, and never says anything
        // else. Falling through to the login is the one action left that changes the answer, since a login
        // stored in the file shadows the variable.
        var toggleLogin = ReactiveCommand.CreateFromTask(async () =>
        {
            if (LoggedIn && await ClearLoginToken()) return;
            StartLogin();
        });
        ToggleLogin = toggleLogin;

        // An unobserved ReactiveCommand exception is rethrown on the UI thread and takes the app with it.
        foreach (var thrown in new[]
                     {clearLogin.ThrownExceptions, triggerLogin.ThrownExceptions, toggleLogin.ThrownExceptions})
        {
            thrown.Subscribe(ex => _logger.LogError(ex, "A Nexus Mods login command failed"))
                .DisposeWith(CompositeDisposable);
        }
    }

    /// <summary>
    ///     Deletes the stored login and re-reads what is left. False when there was no stored login to
    ///     delete, which is not the same as being logged out: an environment-provided credential is still
    ///     there afterwards, and the caller has to do something else about it.
    /// </summary>
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
    ///     Opens the login window, unless one is already open. Two clicks, or the settings tile and
    ///     preflight's Log in action together, would otherwise queue a second window behind the first -
    ///     <c>MainWindowVM</c> serialises browser windows - and open it the moment the user finished with
    ///     the one they were looking at.
    /// </summary>
    private void StartLogin()
    {
        if (Interlocked.Exchange(ref _loginWindowOpen, 1) == 1)
        {
            _logger.LogInformation("A {SiteName} login window is already open", SiteName);
            return;
        }

        try
        {
            var handler = _serviceProvider.GetRequiredService<NexusLoginHandler>();
            handler.Closed += async (_, _) =>
            {
                Interlocked.Exchange(ref _loginWindowOpen, 0);
                await RefreshTokenState();
            };
            ShowBrowserWindow.Send(handler);
        }
        catch (Exception)
        {
            // Nothing was opened, so nothing will close and clear this.
            Interlocked.Exchange(ref _loginWindowOpen, 0);
            throw;
        }
    }

    /// <summary>
    ///     Asks the same question the downloader and preflight ask: <see cref="NexusCredential.CanDownload" />
    ///     over what <see cref="NexusApi.CredentialSource" /> resolves. This tile used to decide for itself -
    ///     a stored token whose OAuth had not expired - which made it a third definition of being logged in:
    ///     a stored API key showed as logged out here while preflight read "Logged in (API key)", and an
    ///     expired token showed as logged out although the next call would have refreshed it in place. An
    ///     account whose refresh Nexus refuses now falls out of all three at once.
    /// </summary>
    private async Task RefreshTokenState()
    {
        try
        {
            LoggedIn = (await _api.CredentialSource()).CanDownload();
        }
        catch (Exception ex)
        {
            // Reading the credential can refresh the token, so this covers a network failure as well as an
            // unreadable store. Nothing usable was established either way.
            _logger.LogError(ex, "Failed to refresh Nexus token state");
            LoggedIn = false;
        }

        _refreshed.OnNext(Unit.Default);
    }
}
