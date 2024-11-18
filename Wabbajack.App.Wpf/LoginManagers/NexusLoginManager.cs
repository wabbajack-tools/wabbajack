using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.Downloaders;
using Wabbajack.DTOs.Logins;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.UserIntervention;

namespace Wabbajack.LoginManagers;

public class NexusLoginManager : ViewModel, ILoginFor<NexusDownloader>
{
    private readonly ILogger<NexusLoginManager> _logger;
    private readonly ITokenProvider<NexusOAuthState> _token;
    private readonly IServiceProvider _serviceProvider;

    public string SiteName { get; } = "Nexus Mods";
    public ICommand TriggerLogin { get; set; }
    public ICommand ClearLogin { get; set; }
    
    public ImageSource Icon { get; set; }
    public Type LoginFor()
    {
        return typeof(NexusDownloader);
    }

    [Reactive]
    public bool HaveLogin { get; set; }
    
    public NexusLoginManager(ILogger<NexusLoginManager> logger, ITokenProvider<NexusOAuthState> token, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _token = token;
        _serviceProvider = serviceProvider;
        Task.Run(async () => await RefreshTokenState());
        
        ClearLogin = ReactiveCommand.CreateFromTask(async () =>
        {
            _logger.LogInformation("Deleting Login information for {SiteName}", SiteName);
            await ClearLoginToken();
        }, this.WhenAnyValue(v => v.HaveLogin));

        Icon = BitmapFrame.Create(
            typeof(NexusLoginManager).Assembly.GetManifestResourceStream("Wabbajack.App.Wpf.LoginManagers.Icons.nexus.png")!);
        
        TriggerLogin = ReactiveCommand.CreateFromTask(async () =>
        {
            _logger.LogInformation("Logging into {SiteName}", SiteName); 
            //MessageBus.Current.SendMessage(new OpenBrowserTab(_serviceProvider.GetRequiredService<NexusLoginHandler>()));
            StartLogin();
        }, this.WhenAnyValue(v => v.HaveLogin).Select(v => !v));
    }

    private async Task ClearLoginToken()
    {
        await _token.Delete();
        await RefreshTokenState();
    }

    private void StartLogin()
    {
        var view = new BrowserWindow(_serviceProvider);
        view.Closed += async (sender, args) => { await RefreshTokenState(); };
        var provider = _serviceProvider.GetRequiredService<NexusLoginHandler>();
        view.DataContext = provider;
        view.Show();
    }

    private async Task RefreshTokenState()
    {
        var token = await _token.Get();
            
        HaveLogin = _token.HaveToken() && !(token?.OAuth?.IsExpired ?? true);
    }
}