using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
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
        
        ClearLogin = ReactiveCommand.CreateFromTask(async () =>
        {
            _logger.LogInformation("Deleting Login information for {SiteName}", SiteName);
            await ClearLoginToken();
        }, this.WhenAnyValue(v => v.LoggedIn));

        Icon = (DrawingImage)Application.Current.Resources["NexusLogo"];
        
        TriggerLogin = ReactiveCommand.CreateFromTask(async () =>
        {
            _logger.LogInformation("Logging into {SiteName}", SiteName); 
            StartLogin();
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

    private void StartLogin()
    {
        var handler = _serviceProvider.GetRequiredService<NexusLoginHandler>();
        handler.Closed += async (_, _) => await RefreshTokenState();
        ShowBrowserWindow.Send(handler);
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
