using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CG.Web.MegaApiClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.Downloaders;
using Wabbajack.Downloaders.ModDB;
using Wabbajack.DTOs.Logins;
using Wabbajack.Messages;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.UserIntervention;
using static CG.Web.MegaApiClient.MegaApiClient;

namespace Wabbajack.LoginManagers;

public class MegaLoginManager : ViewModel, ILoginFor<MegaDownloader>
{
    private readonly ILogger<MegaLoginManager> _logger;
    private readonly ITokenProvider<MegaToken> _token;
    private readonly MegaApiClient _apiClient;

    public string SiteName { get; } = "MEGA";
    public ICommand TriggerLogin { get; set; }
    public ICommand ClearLogin { get; set; }
    public ICommand ToggleLogin { get; set; }
    
    public ImageSource Icon { get; set; }
    public Type LoginFor()
    {
        return typeof(MegaDownloader);
    }

    [Reactive]
    public bool LoggedIn { get; set; }
    
    public MegaLoginManager(ILogger<MegaLoginManager> logger, ITokenProvider<MegaToken> token, MegaApiClient apiClient)
    {
        _logger = logger;
        _token = token;
        _apiClient = apiClient;
        
        ClearLogin = ReactiveCommand.CreateFromTask(async () =>
        {
            _logger.LogInformation("Deleting Login information for {SiteName}", SiteName);
            await ClearLoginToken();
        }, this.WhenAnyValue(v => v.LoggedIn));

        Icon = BitmapFrame.Create(
            typeof(MegaLoginManager).Assembly.GetManifestResourceStream("Wabbajack.App.Wpf.LoginManagers.Icons.mega.png")!);
        
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

        MessageBus.Current.Listen<LoggedIntoMega>()
            .Subscribe(async (loggedIntoMega) => await UpdateToken(loggedIntoMega?.Login))
            .DisposeWith(CompositeDisposable);

        LoggedIn = _token.HaveToken();
    }

    private async Task ClearLoginToken()
    {
        try
        {
            if(_apiClient.IsLoggedIn)
                await _apiClient.LogoutAsync();
        }
        catch(Exception ex)
        {
            _logger.LogError("Failed to log out of MEGA: {ex}", ex.ToString());
        }
        await _token.Delete();
        LoggedIn = _token.HaveToken();
    }

    private void StartLogin()
    {
        ShowFloatingWindow.Send(FloatingScreenType.MegaLogin);
    }

    private async Task UpdateToken(AuthInfos login)
    {
        try
        {
            await _token.SetToken(new MegaToken() { Login = login });
        }
        catch(Exception ex)
        {
            _logger.LogError("Failed to refresh Mega token state: {ex}", ex.ToString());
        }

        LoggedIn = _token.HaveToken();
    }
}