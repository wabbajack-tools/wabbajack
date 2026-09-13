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
using Wabbajack.UserIntervention;

namespace Wabbajack.LoginManagers;

public partial class NexusLoginManager : ViewModel, ILoginFor<NexusDownloader>
{
    private readonly ILogger<NexusLoginManager> _logger;
    private readonly ITokenProvider<NexusOAuthState> _token;
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

    public NexusLoginManager(ILogger<NexusLoginManager> logger, ITokenProvider<NexusOAuthState> token, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _token = token;
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

    private async Task RefreshTokenState()
    {
        NexusOAuthState token = null;
        try
        {
            token = await _token.Get();
        }
        catch(Exception ex)
        {
            _logger.LogError("Failed to refresh Nexus token state: {ex}", ex.ToString());
        }
            
        LoggedIn = _token.HaveToken() && !(token?.OAuth?.IsExpired ?? true);
        _refreshed.OnNext(Unit.Default);
    }
}
