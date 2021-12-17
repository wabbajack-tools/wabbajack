using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wabbajack.BuildServer;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.Server.DTOs;
using Wabbajack.Server.Services;

namespace Wabbajack.Server.Controllers;

[ApiController]
[Authorize(Roles = "Author")]
[Route("/patches")]
public class Patches : ControllerBase
{
    private readonly AppSettings _settings;
    private readonly DiscordWebHook _discord;
    private readonly DTOSerializer _dtos;

    public Patches(AppSettings appSettings, DiscordWebHook discord, DTOSerializer dtos)
    {
        _settings = appSettings;
        _discord = discord;
        _dtos = dtos;
    }

    [HttpPost]
    public async Task<IActionResult> WritePart(CancellationToken token, [FromQuery] string name, [FromQuery] long start)
    {
        var path = GetPath(name);
        if (!path.FileExists())
        {
            
            var user = User.FindFirstValue(ClaimTypes.Name)!;
            await _discord.Send(Channel.Ham,
                new DiscordMessage {Content = $"{user} is uploading a new forced-healing patch file"});
        }

        await using var file = path.Open(FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);
        file.Position = start;
        var hash = await Request.Body.HashingCopy(file, token);
        await file.FlushAsync(token);
        return Ok(hash.ToHex());
    }

    private AbsolutePath GetPath(string name)
    {
        return _settings.PatchesFilesFolder.ToAbsolutePath().Combine(name);
    }

    [HttpGet]
    [Route("list")]
    public async Task<IActionResult> ListPatches(CancellationToken token)
    {
        var root = _settings.PatchesFilesFolder.ToAbsolutePath();
        var files = root.EnumerateFiles()
            .ToDictionary(f => f.RelativeTo(root).ToString(), f => f.Size());
        return Ok(_dtos.Serialize(files));
    }

    [HttpDelete]
    public async Task<IActionResult> DeletePart([FromQuery] string name)
    {
        var user = User.FindFirstValue(ClaimTypes.Name)!;
        await _discord.Send(Channel.Ham,
            new DiscordMessage {Content = $"{user} is deleting a new forced-healing patch file"});
        
        GetPath(name).Delete();
        return Ok(name);
    }
    
}