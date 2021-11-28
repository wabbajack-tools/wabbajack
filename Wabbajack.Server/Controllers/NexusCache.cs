using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Wabbajack.Networking.NexusApi;
using Wabbajack.Networking.NexusApi.DTOs;

namespace Wabbajack.BuildServer.Controllers;

//[Authorize]
[ApiController]
[Authorize(Roles = "User")]
[Route("/v1/games/")]
public class NexusCache : ControllerBase
{
    private readonly NexusApi _api;
    private readonly ILogger<NexusCache> _logger;
    private AppSettings _settings;
    private readonly HttpClient _client;

    public NexusCache(ILogger<NexusCache> logger,AppSettings settings, HttpClient client)
    {
        _settings = settings;
        _logger = logger;
        _client = client;
    }

    private async Task ForwardToNexus(HttpRequest src)
    {
        _logger.LogInformation("Nexus Cache Forwarding: {path}", src.Path);
        var request = new HttpRequestMessage(HttpMethod.Get, (Uri?)new Uri("https://api.nexusmods.com/" + src.Path));
        request.Headers.Add("apikey", (string?)src.Headers["apikey"]);
        request.Headers.Add("User-Agent", (string?)src.Headers.UserAgent);
        using var response = await _client.SendAsync(request);
        Response.Headers.ContentType = "application/json";
        Response.StatusCode = (int)response.StatusCode;
        await response.Content.CopyToAsync(Response.Body);
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
    public async Task GetModInfo(string GameName, long ModId)
    {
        await ForwardToNexus(Request);
    }

    [HttpGet]
    [Route("{GameName}/mods/{ModId}/files.json")]
    public async Task GetModFiles(string GameName, long ModId)
    {
        await ForwardToNexus(Request);
    }

    [HttpGet]
    [Route("{GameName}/mods/{ModId}/files/{FileId}.json")]
    public async Task GetModFile(string GameName, long ModId, long FileId)
    {
        await ForwardToNexus(Request);
    }
}