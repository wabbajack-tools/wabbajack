using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Wabbajack.Common;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Networking.NexusApi.DTOs;
using Wabbajack.Paths.IO;
using Wabbajack.Server.Services;

namespace Wabbajack.BuildServer.Controllers;

//[Authorize]
[ApiController]
[Authorize(Roles = "User")]
[Route("/v1/games/")]
public class NexusCache : ControllerBase
{
    private readonly ILogger<NexusCache> _logger;
    private readonly HttpClient _client;
    private readonly DTOSerializer _dtos;
    private readonly NexusCacheManager _cache;

    public NexusCache(ILogger<NexusCache> logger, HttpClient client, NexusCacheManager cache, DTOSerializer dtos)
    {
        _logger = logger;
        _client = client;
        _cache = cache;
        _dtos = dtos;
    }

    private async Task<T> ForwardRequest<T>(HttpRequest src, CancellationToken token)
    {
        _logger.LogInformation("Nexus Cache Forwarding: {path}", src.Path);
        var request = new HttpRequestMessage(HttpMethod.Get, (Uri?)new Uri("https://api.nexusmods.com/" + src.Path));
        request.Headers.Add("apikey", (string?)src.Headers["apikey"]);
        request.Headers.Add("User-Agent", (string?)src.Headers.UserAgent);
        using var response = await _client.SendAsync(request, token);
        return (await JsonSerializer.DeserializeAsync<T>(await response.Content.ReadAsStreamAsync(token), _dtos.Options, token))!;
    }

    /// <summary>
    ///     Looks up the mod details for a given Gamename/ModId pair. If the entry is not found in the cache it will
    ///     be requested from the server (using the caller's Nexus API key if provided).
    /// </summary>
    /// <param name="db"></param>
    /// <param name="GameName">The Nexus game name</param>
    /// <param name="ModId">The Nexus mod id</param>
    /// <returns>A Mod Info result</returns>
    [HttpGet]
    [Route("{GameName}/mods/{ModId}.json")]
    public async Task GetModInfo(string GameName, long ModId, CancellationToken token)
    {
        var key = $"modinfo_{GameName}_{ModId}";
        await ReturnCachedResult<ModInfo>(key, token);
    }

    private async Task ReturnCachedResult<T>(string key, CancellationToken token)
    {
        key = key.ToLowerInvariant();
        var cached = await _cache.GetCache<T>(key, token);
        if (cached == null)
        {
            var returned = await ForwardRequest<T>(Request, token);
            await _cache.SaveCache(key, returned, token);
            Response.StatusCode = 200;
            Response.ContentType = "application/json";
            await JsonSerializer.SerializeAsync(Response.Body, returned, _dtos.Options, cancellationToken: token);
            return;
        }

        await JsonSerializer.SerializeAsync(Response.Body, cached, _dtos.Options, cancellationToken: token);
    }

    [HttpGet]
    [Route("{GameName}/mods/{ModId}/files.json")]
    public async Task GetModFiles(string GameName, long ModId, CancellationToken token)
    {
        var key = $"modfiles_{GameName}_{ModId}";
        await ReturnCachedResult<ModFiles>(key, token);
    }

    [HttpGet]
    [Route("{GameName}/mods/{ModId}/files/{FileId}.json")]
    public async Task GetModFile(string GameName, long ModId, long FileId, CancellationToken token)
    {
        var key = $"modfile_{GameName}_{ModId}_{FileId}";
        await ReturnCachedResult<ModFile>(key, token);
    }
}