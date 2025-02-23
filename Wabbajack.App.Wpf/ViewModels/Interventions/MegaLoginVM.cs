using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using System.Windows.Input;
using CG.Web.MegaApiClient;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.DTOs.Logins;
using Wabbajack.Messages;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.RateLimiter;
using Wabbajack.Services.OSIntegrated.TokenProviders;
using static CG.Web.MegaApiClient.MegaApiClient;

namespace Wabbajack;

public class MegaLoginVM : ViewModel
{

    private readonly ILogger<MegaLoginVM> _logger;
    private readonly MegaTokenProvider _tokenProvider;
    private readonly MegaApiClient _apiClient;

    public ICommand CloseCommand { get; }

    [Reactive] public double UploadProgress { get; set; }
    [Reactive] public string FileUrl { get; set; }
    public FilePickerVM Picker { get;}
    
    [Reactive] public string Email { get; set; }
    [Reactive] public string Password { get; set; }
    [Reactive] public AuthInfos Login { get; private set; }

    public MegaLoginVM(ILogger<MegaLoginVM> logger, MegaTokenProvider tokenProvider, Client wjClient, SettingsVM vm, MegaApiClient apiClient)
    {
        _logger = logger;
        _tokenProvider = tokenProvider;
        _apiClient = apiClient;
        CloseCommand = ReactiveCommand.Create(async () =>
        {
            ShowFloatingWindow.Send(FloatingScreenType.None);
            var login = await _apiClient.GenerateAuthInfosAsync(Email, Password);
            // Clearing unencrypted data out of memory
            Email = "";
            Password = "";
            LoggedIntoMega.Send(login);
        });
    }

}
