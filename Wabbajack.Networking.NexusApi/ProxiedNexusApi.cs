using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.DTOs;
using Wabbajack.DTOs.Logins;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.Networking.NexusApi.DTOs;
using Wabbajack.RateLimiter;
using ClientConfiguration = Wabbajack.Networking.WabbajackClientApi.Configuration;

namespace Wabbajack.Networking.NexusApi;

public class ProxiedNexusApi : NexusApi
{
    private readonly ITokenProvider<WabbajackApiState> _apiState;
    private readonly ClientConfiguration _wabbajackClientConfiguration;

    public HashSet<string> ProxiedEndpoints = new()
    {
        Endpoints.ModInfo,
        Endpoints.ModFiles,
        Endpoints.ModFile
    };

    public ProxiedNexusApi(ITokenProvider<NexusOAuthState> apiKey, ILogger<ProxiedNexusApi> logger, HttpClient client,
        IResource<HttpClient> limiter,
        ApplicationInfo appInfo, JsonSerializerOptions jsonOptions, ITokenProvider<WabbajackApiState> apiState,
        ClientConfiguration wabbajackClientConfiguration)
        : base(apiKey, logger, client, limiter, appInfo, jsonOptions)
    {
        _apiState = apiState;
        _wabbajackClientConfiguration = wabbajackClientConfiguration;
    }

    protected override async ValueTask<HttpRequestMessage> GenerateMessage(HttpMethod method, string uri,
        params object?[] parameters)
    {
        var msg = await base.GenerateMessage(method, uri, parameters);
        if (ProxiedEndpoints.Contains(uri))
            msg.RequestUri = new Uri($"https://build.wabbajack.org/{string.Format(uri, parameters)}");
        msg.Headers.Add(_wabbajackClientConfiguration.MetricsKeyHeader, (await _apiState.Get())!.MetricsKey);
        return msg;
    }

    protected override ResponseMetadata ParseHeaders(HttpResponseMessage result)
    {
        if (result.RequestMessage!.RequestUri!.Host == "build.wabbajack.org")
            return new ResponseMetadata {IsReal = false};
        return base.ParseHeaders(result);
    }
}