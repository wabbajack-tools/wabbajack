using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Wabbajack.Common;
using Wabbajack.Networking.GitHub;
using Wabbajack.Server.DTOs;
using Wabbajack.Server.Services;

namespace Wabbajack.Server.Controllers;

[Authorize(Roles = "Author")]
[Route("/github")]
public class Github : ControllerBase
{
    private readonly Client _client;
    private readonly ILogger<Github> _logger;
    private readonly DiscordWebHook _discord;

    public Github(ILogger<Github> logger, Client client, DiscordWebHook discord)
    {
        _client = client;
        _logger = logger;
        _discord = discord;
    }

    [HttpGet]
    public async Task GetContent([FromQuery] string owner, [FromQuery] string repo, [FromQuery] string path)
    {
        var (sha, content) = await _client.GetData(owner, repo, path);
        Response.StatusCode = 200;
        Response.Headers.Add("x-content-sha", sha);
        await Response.WriteAsync(content);
    }

    [HttpPost]
    public async Task<IActionResult> SetContent([FromQuery] string owner, [FromQuery] string repo, [FromQuery] string path, [FromQuery] string oldSha)
    {
        var user = User.FindFirstValue(ClaimTypes.Name)!;
        _logger.LogInformation("Updating {Owner}/{Repo}/{Path} on behalf of {User}", owner, repo, path, user);
        
        await _discord.Send(Channel.Ham,
            new DiscordMessage {Content = $"Updating {owner}/{repo}/{path} on behalf of {user}"});
        
        var content = Encoding.UTF8.GetString(await Request.Body.ReadAllAsync());
        await _client.PutData(owner, repo, path, $"Update on behalf of {user}", content, oldSha);
        return Ok();
    }
    
}