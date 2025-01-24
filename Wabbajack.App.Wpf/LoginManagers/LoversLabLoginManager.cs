using System;
using System.Reactive.Linq;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.Common;
using Wabbajack.Downloaders.IPS4OAuth2Downloader;
using Wabbajack.DTOs.Logins;
using Wabbajack.Messages;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.UserIntervention;

namespace Wabbajack.LoginManagers;

public class LoversLabLoginManager : ViewModel, ILoginFor<LoversLabDownloader>
{
    private readonly ILogger<LoversLabLoginManager> _logger;
    private readonly ITokenProvider<LoversLabLoginState> _token;
    private readonly IServiceProvider _serviceProvider;

    public string SiteName { get; } = "Lovers Lab";
    public ICommand TriggerLogin { get; set; }
    public ICommand ClearLogin { get; set; }
    public ICommand ToggleLogin { get; set; }
    
    public ImageSource Icon { get; set; }
    public Type LoginFor()
    {
        return typeof(LoversLabDownloader);
    }

    [Reactive]
    public bool LoggedIn { get; set; }
    
    public LoversLabLoginManager(ILogger<LoversLabLoginManager> logger, ITokenProvider<LoversLabLoginState> token, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _token = token;
        _serviceProvider = serviceProvider;
        RefreshTokenState();
        
        ClearLogin = ReactiveCommand.CreateFromTask(async () =>
        {
            _logger.LogInformation("Deleting Login information for {SiteName}", SiteName);
            await _token.Delete();
            RefreshTokenState();
        }, this.WhenAnyValue(v => v.LoggedIn));

        Icon = BitmapFrame.Create(
            typeof(LoversLabLoginManager).Assembly.GetManifestResourceStream("Wabbajack.App.Wpf.LoginManagers.Icons.lovers_lab.png")!);
        
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
    
    private void StartLogin()
    {
        var handler = _serviceProvider.GetRequiredService<LoversLabLoginHandler>();
        handler.Closed += (sender, args) => { RefreshTokenState(); };
        ShowBrowserWindow.Send(handler);
    }

    private void RefreshTokenState()
    {
        LoggedIn = _token.HaveToken();
    }
}