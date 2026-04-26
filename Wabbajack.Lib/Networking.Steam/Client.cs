using System.Collections.Concurrent;
using System.IO.Compression;
using System.Security;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic.CompilerServices;
using SteamKit2;
using SteamKit2.CDN;
using SteamKit2.Internal;
using Wabbajack.Common;
using Wabbajack.DTOs.Interventions;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.Networking.Steam.DTOs;
using Wabbajack.Networking.Steam.UserInterventions;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;

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
    private bool _haveSigFile;

    public TaskCompletionSource _licenseRequest = new();
    private readonly SteamApps _steamApps;
    private readonly DTOSerializer _dtos;
    private Server[] _cdnServers = Array.Empty<Server>();
    private readonly IResource<HttpClient> _limiter;

    public SteamApps.LicenseListCallback.License[] Licenses { get; private set; }

    public ConcurrentDictionary<uint, ulong> PackageTokens { get; } = new();

    public ConcurrentDictionary<uint, SteamApps.PICSProductInfoCallback.PICSProductInfo?> PackageInfos { get; } = new();

    public ConcurrentDictionary<uint, (SteamApps.PICSProductInfoCallback.PICSProductInfo ProductInfo, AppInfo AppInfo)> AppInfo { get; } =
        new();

    public ConcurrentDictionary<uint, ulong> AppTokens { get; } = new();
    
    
    public ConcurrentDictionary<uint, byte[]> DepotKeys { get; } = new();


    public Client(ILogger<Client> logger, HttpClient client, ITokenProvider<SteamLoginState> token,
        IUserInterventionHandler interventionHandler, DTOSerializer dtos, IResource<HttpClient> limiter)
    {
        _logger = logger;
        _httpClient = client;
        _dtos = dtos;
        _interventionHandler = interventionHandler;
        _limiter = limiter;
        _client = new SteamClient(SteamConfiguration.Create(c =>
        {
            c.WithProtocolTypes(ProtocolTypes.WebSocket);
            c.WithUniverse(EUniverse.Public);
        }));
        

        _cancellationSource = new CancellationTokenSource();
        
        _token = token;

        _manager = new CallbackManager(_client);

        _steamUser = _client.GetHandler<SteamUser>()!;
        _steamApps = _client.GetHandler<SteamApps>()!;
        
        _manager.Subscribe<SteamClient.ConnectedCallback>( OnConnected );
        _manager.Subscribe<SteamClient.DisconnectedCallback>( OnDisconnected );

        _manager.Subscribe<SteamUser.LoggedOnCallback>( OnLoggedOn );
        _manager.Subscribe<SteamUser.LoggedOffCallback>( OnLoggedOff );

        _manager.Subscribe<SteamApps.LicenseListCallback>(OnLicenseList);
        
        _manager.Subscribe<SteamUser.UpdateMachineAuthCallback>( OnUpdateMachineAuthCallback );

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

    private void OnLicenseList(SteamApps.LicenseListCallback obj)
    {       
        if (obj.Result != EResult.OK)
        {
            _licenseRequest.TrySetException(new SteamException("While getting game information", obj.Result, EResult.Invalid));
        }
        _logger.LogInformation("Steam has provided game information");
        Licenses = obj.LicenseList.ToArray();
        _licenseRequest.TrySetResult();
    }

    private void OnUpdateMachineAuthCallback(SteamUser.UpdateMachineAuthCallback callback)
    {
        Task.Run(async () =>
        {
            int fileSize;
            byte[] sentryHash;
            
            _logger.LogInformation("Got Steam machine auth info");
            
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

            _logger.LogInformation("Logged into Steam");
            if (_haveSigFile) 
                _loginTask.SetResult();
        });
    }

    private void OnDisconnected(SteamClient.DisconnectedCallback obj)
    {
        _logger.LogInformation("Logged out");
    }

    private void OnConnected(SteamClient.ConnectedCallback obj)
    {
        Task.Run(async () =>
        {
            var state = (await _token.Get())!;
            _logger.LogInformation("Connected to Steam, logging in");

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

            _steamUser.LogOn(new SteamUser.LogOnDetails
            {
                Username = state.User,
                Password = state.Password,
                AuthCode = _authCode,
                TwoFactorCode = _twoFactorCode,
                SentryFileHash = sentryHash,
                RequestSteam2Ticket = true
            });
            
            // Reset the codes so we don't use them again
            _authCode = null;
            _twoFactorCode = null;
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

    public async Task Login(TaskCompletionSource? tcs = null)
    {
        _loginTask = tcs ?? new TaskCompletionSource();
        _logger.LogInformation("Attempting login");
        _client.Connect();

        await _loginTask.Task; 
        await _licenseRequest.Task;
    }

    public async Task<Dictionary<uint, SteamApps.PICSProductInfoCallback.PICSProductInfo?>> GetPackageInfos(IEnumerable<uint> packageIds)
    {
        var packages = packageIds.Where(id => !PackageInfos.ContainsKey(id)).ToList();

        if (packages.Count > 0)
        {
            var packageRequests = new List<SteamApps.PICSRequest>();

            foreach (var package in packages)
            {
                var request = new SteamApps.PICSRequest(package);
                if (PackageTokens.TryGetValue(package, out var token))
                {
                    request.AccessToken = token;
                }

                packageRequests.Add(request);
            }

            _logger.LogInformation("Requesting {Count} package infos", packageRequests.Count);

            var results = await _steamApps.PICSGetProductInfo(new List<SteamApps.PICSRequest>(), packageRequests);

            if (results.Failed)
                throw new SteamException("Exception getting product info", EResult.Invalid, EResult.Invalid);

            foreach (var packageInfo in results.Results!)
            {
                foreach (var package in packageInfo.Packages.Select(v => v.Value))
                {
                    PackageInfos[package.ID] = package;
                }

                foreach (var package in packageInfo.UnknownPackages)
                {
                    PackageInfos[package] = null;
                }
            }
        }

        return packages.Distinct().ToDictionary(p => p, p => PackageInfos[p]);

    }

    public async Task<AppInfo> GetAppInfo(uint appId)
    {
        if (AppInfo.TryGetValue(appId, out var info))
            return info.AppInfo;

        var result = await _steamApps.PICSGetAccessTokens(new List<uint> {appId}, new List<uint>());
        
        if (result.AppTokensDenied.Contains(appId))
            throw new SteamException($"Cannot get app token for {appId}", EResult.Invalid, EResult.Invalid);

        foreach (var token in result.AppTokens)
        {
            AppTokens[token.Key] = token.Value;
        }

        var request = new SteamApps.PICSRequest(appId);
        if (AppTokens.ContainsKey(appId))
        {
            request.AccessToken = AppTokens[appId];
        }

        var appResult = await _steamApps.PICSGetProductInfo(new List<SteamApps.PICSRequest> {request},
            new List<SteamApps.PICSRequest>());

        if (appResult.Failed)
            throw new SteamException($"Error getting app info for {appId}", EResult.Invalid, EResult.Invalid);

        foreach (var (_, value) in appResult.Results!.SelectMany(v => v.Apps))
        {
            var translated = KeyValueTranslator.Translate<AppInfo>(value.KeyValues, _dtos);
            AppInfo[value.ID] = (value, translated);
        }

        return AppInfo[appId].AppInfo;
    }

    public async Task<Server[]> LoadCDNServers()
    {
        if (_cdnServers.Length > 0) return _cdnServers;
        _logger.LogInformation("Loading CDN servers");
        _cdnServers = (await ContentServerDirectoryService.LoadAsync(_client.Configuration)).ToArray();
        _logger.LogInformation("{Count} servers found", _cdnServers.Length);
        
        return _cdnServers;
    }

    public async Task<DepotManifest> GetAppManifest(uint appId, uint depotId, ulong manifestId)
    {
        await LoadCDNServers();
        
        var manifest = await CircuitBreaker.WithAutoRetryAsync<DepotManifest, HttpRequestException>(_logger, async () =>
        {
            
            var client = _cdnServers.First();
            var uri = new UriBuilder
            {
                Host = client.Host,
                Port = client.Port,
                Scheme = client.Protocol.ToString(),
                Path = $"depot/{depotId}/manifest/{manifestId}/5"
            }.Uri;
            
            var rawData = await _httpClient.GetByteArrayAsync(uri);

            using var zip = new ZipArchive(new MemoryStream(rawData));
            var firstEntry = zip.Entries.First();
            var data = new MemoryStream();
            await using var entryStream = firstEntry.Open();
            await entryStream.CopyToAsync(data);
            return DepotManifest.Deserialize(data.ToArray());
        });


        if (manifest.FilenamesEncrypted)
            manifest.DecryptFilenames(await GetDepotKey(depotId, appId));

        return manifest;
    }
    
    
    public async ValueTask<byte[]> GetDepotKey(uint depotId, uint appId)
    {
        if (DepotKeys.ContainsKey(depotId))
            return DepotKeys[depotId];
        
        _logger.LogInformation("Requesting Depot Key for {DepotId}", depotId);

        var result = await _steamApps.GetDepotDecryptionKey(depotId, appId);
        if (result.Result != EResult.OK)
            throw new SteamException($"Error getting Depot Key for {depotId} {appId}", result.Result, EResult.Invalid);

        DepotKeys[depotId] = result.DepotKey;
        return result.DepotKey;
    }
    
    private static readonly Random _random = new();

    private Server RandomServer()
    {
        return _cdnServers[_random.Next(0, _cdnServers.Length)];
    }

    public async Task Download(uint appId, uint depotId, ulong manifest, DepotManifest.FileData fileData, AbsolutePath output, 
        CancellationToken token, IJob? parentJob = null)
    {
        await LoadCDNServers();

        var depotKey = await GetDepotKey(depotId, appId);

        await using var os = output.Open(FileMode.Create, FileAccess.Write, FileShare.Read);

        await fileData.Chunks.OrderBy(c => c.Offset)
            .PMapAll(async chunk =>
            {
                async Task<DepotChunk> AttemptDownload(DepotManifest.ChunkData chunk)
                {
                    var client = RandomServer();
                    using var job = await _limiter.Begin($"Downloading chunk of {fileData.FileName}",
                        chunk.CompressedLength, token);

                    var chunkId = chunk.ChunkID!.ToHex();


                    var uri = new UriBuilder
                    {
                        Host = client.Host,
                        Port = client.Port,
                        Scheme = client.Protocol.ToString(),
                        Path = $"depot/{depotId}/chunk/{chunkId}"
                    }.Uri;

                    var data = await _httpClient.GetByteArrayAsync(uri, token);
                    await job.Report(data.Length, token);

                    if (parentJob != null)
                        await parentJob.Report(data.Length, token);

                    var chunkData = new DepotChunk(chunk, data);
                    chunkData.Process(depotKey);

                    return chunkData;
                }

                return await CircuitBreaker.WithAutoRetryAsync<DepotChunk, HttpRequestException>(_logger, () => AttemptDownload(chunk));
            }).Do(async data =>
            {
                await os.WriteAsync(data.Data, token);
                
            });
    }
}