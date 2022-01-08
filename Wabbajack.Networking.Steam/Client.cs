using System.Security;
using Microsoft.Extensions.Logging;
using SteamKit2;
using Wabbajack.DTOs.Interventions;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.Networking.Steam.UserInterventions;

namespace Wabbajack.Networking.Steam;

public class Client : IDisposable
{
    private readonly ILogger<Client> _logger;
    private readonly HttpClient _httpClient;
    private readonly SteamClient _client;
    private readonly SteamUser _steamUser;
    private readonly CallbackManager _manager;
    private readonly ITokenProvider<SteamLoginState> _token;
    private TaskCompletionSource _loginTask;
    private TaskCompletionSource _connectTask;
    private readonly CancellationTokenSource _cancellationSource;

    private string? _twoFactorCode;
    private string? _authCode;
    private readonly IUserInterventionHandler _interventionHandler;
    private bool _isConnected;
    private bool _isLoggedIn;
    private bool _haveSigFile;

    public Client(ILogger<Client> logger, HttpClient client, ITokenProvider<SteamLoginState> token,
        IUserInterventionHandler interventionHandler)
    {
        _logger = logger;
        _httpClient = client;
        _interventionHandler = interventionHandler;
        _client = new SteamClient(SteamConfiguration.Create(c =>
        {
            c.WithHttpClientFactory(() => client);
            c.WithProtocolTypes(ProtocolTypes.WebSocket);
            c.WithUniverse(EUniverse.Public);
        }));
        

        _cancellationSource = new CancellationTokenSource();
        
        _token = token;

        _manager = new CallbackManager(_client);

        _steamUser = _client.GetHandler<SteamUser>()!;
        
        _manager.Subscribe<SteamClient.ConnectedCallback>( OnConnected );
        _manager.Subscribe<SteamClient.DisconnectedCallback>( OnDisconnected );

        _manager.Subscribe<SteamUser.LoggedOnCallback>( OnLoggedOn );
        _manager.Subscribe<SteamUser.LoggedOffCallback>( OnLoggedOff );
        
        _manager.Subscribe<SteamUser.UpdateMachineAuthCallback>( OnUpdateMachineAuthCallback );

        _isConnected = false;
        _isLoggedIn = false;
        _haveSigFile = false;

        new Thread(() =>
        {
            while (!_cancellationSource.IsCancellationRequested)
            {
                _manager.RunWaitCallbacks(TimeSpan.FromMilliseconds(250));
            }
        })
        {
            Name = "Steam Client callback runner",
            IsBackground = true
        }
        .Start();
    }

    private void OnUpdateMachineAuthCallback(SteamUser.UpdateMachineAuthCallback callback)
    {
        Task.Run(async () =>
        {
            int fileSize;
            byte[] sentryHash;
            
            var token = await _token.Get();

            var ms = new MemoryStream();
            
            if (token?.SentryFile != null)
                await ms.WriteAsync(token.SentryFile);
            
            ms.Seek(callback.Offset, SeekOrigin.Begin);
            ms.Write(callback.Data, 0, callback.BytesToWrite);
            fileSize = (int) ms.Length;

            token!.SentryFile = ms.ToArray();
            sentryHash = CryptoHelper.SHAHash(token.SentryFile);

            await _token.SetToken(token);
            

            _steamUser.SendMachineAuthResponse(new SteamUser.MachineAuthDetails
            {
                JobID = callback.JobID,
                FileName = callback.FileName,

                BytesWritten = callback.BytesToWrite,
                FileSize = token.SentryFile.Length,
                Offset = callback.Offset,
                Result = EResult.OK,
                LastError = 0,
                OneTimePassword = callback.OneTimePassword,
                SentryFileHash = sentryHash
            });

            _haveSigFile = true;
            _loginTask.TrySetResult();
        });
    }

    private void OnLoggedOff(SteamUser.LoggedOffCallback obj)
    {
        _isLoggedIn = false;
    }

    private void OnLoggedOn(SteamUser.LoggedOnCallback callback)
    {
        Task.Run(async () =>
        {
            var isSteamGuard = callback.Result == EResult.AccountLogonDenied;
            var is2FA = callback.Result == EResult.AccountLoginDeniedNeedTwoFactor;

            if (isSteamGuard || is2FA)
            {
                _logger.LogInformation("Account is SteamGuard protected");
                if (is2FA)
                {
                    var intervention = new GetAuthCode(GetAuthCode.AuthType.TwoFactorAuth);
                    _interventionHandler.Raise(intervention);
                    _twoFactorCode = await intervention.Task;
                }
                else
                {
                    var intervention = new GetAuthCode(GetAuthCode.AuthType.EmailCode);
                    _interventionHandler.Raise(intervention);
                    _authCode = await intervention.Task;
                }
                
                var tcs = Login(_loginTask);
                return;
            }

            if (callback.Result != EResult.OK)
            {
                _loginTask.SetException(new SteamException("Unable to log in", callback.Result, callback.ExtendedResult));
                return;
            }

            _isLoggedIn = true;
            _logger.LogInformation("Logged into Steam");
            if (_haveSigFile) 
                _loginTask.SetResult();
        });
    }

    private void OnDisconnected(SteamClient.DisconnectedCallback obj)
    {
        _isConnected = false;
        _logger.LogInformation("Logged out");
    }

    private void OnConnected(SteamClient.ConnectedCallback obj)
    {
        Task.Run(async () =>
        {
            var state = (await _token.Get())!;
            _logger.LogInformation("Connected to Steam, logging in as {User}", state.User);

            byte[]? sentryHash = null;
            
            
            if (state.SentryFile != null)
            {
                _logger.LogInformation("Existing login keys found, reusing");
                sentryHash = CryptoHelper.SHAHash(state.SentryFile);
                _haveSigFile = true;
            }
            else
            {
                _haveSigFile = false;
            }
            

            _isConnected = true;
            
            _steamUser.LogOn(new SteamUser.LogOnDetails()
            {
                Username = state.User,
                Password = state.Password,
                AuthCode = _authCode,
                TwoFactorCode = _twoFactorCode,
                SentryFileHash = sentryHash
            });
        });
    }
    
    public Task Connect()
    {
        _connectTask = new TaskCompletionSource();

        _client.Connect();
        return _connectTask.Task;
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _cancellationSource.Cancel();
        _cancellationSource.Dispose();
    }

    public Task Login(TaskCompletionSource? tcs = null)
    {
        _loginTask = tcs ?? new TaskCompletionSource();
        _logger.LogInformation("Attempting login");
        _client.Connect();

        return _loginTask.Task;
    }
}