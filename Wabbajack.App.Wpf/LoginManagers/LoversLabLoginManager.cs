using System;
using System.Drawing;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
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
    
    public ImageSource Icon { get; set; }
    public Type LoginFor()
    {
        return typeof(LoversLabDownloader);
    }

    [Reactive]
    public bool HaveLogin { get; set; }
    
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
        }, this.WhenAnyValue(v => v.HaveLogin));

        Icon = BitmapFrame.Create(
            typeof(LoversLabLoginManager).Assembly.GetManifestResourceStream("Wabbajack.App.Wpf.LoginManagers.Icons.lovers_lab.png")!);
        
        TriggerLogin = ReactiveCommand.CreateFromTask(async () =>
        {
            _logger.LogInformation("Logging into {SiteName}", SiteName);
            StartLogin();
        }, this.WhenAnyValue(v => v.HaveLogin).Select(v => !v));
    }
    
    private void StartLogin()
    {
        var view = new BrowserWindow(_serviceProvider);
        view.Closed += (sender, args) => { RefreshTokenState(); };
        var provider = _serviceProvider.GetRequiredService<LoversLabLoginHandler>();
        view.DataContext = provider;
        view.Show();
    }

    private void RefreshTokenState()
    {
        HaveLogin = _token.HaveToken();
    }
}