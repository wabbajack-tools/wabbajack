

using System.IO.Compression;
using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Wabbajack.BuildServer;
using Wabbajack.Common;
using Wabbajack.DTOs.CDN;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.Server.DataModels;
using Wabbajack.Server.DTOs;
using Wabbajack.Server.Services;

namespace Wabbajack.Server.Controllers;

[Authorize(Roles = "Author")]
[Route("/mirrored_files")]
public class MirroredFiles : ControllerBase
{
    private readonly DTOSerializer _dtos;

    private readonly DiscordWebHook _discord;
    private readonly ILogger<MirroredFiles> _logger;
    private readonly AppSettings _settings;
    
    public AbsolutePath MirrorFilesLocation => _settings.MirrorFilesFolder.ToAbsolutePath();


    public MirroredFiles(ILogger<MirroredFiles> logger, AppSettings settings, DiscordWebHook discord,
        DTOSerializer dtos)
    {
        _logger = logger;
        _settings = settings;
        _discord = discord;
        _dtos = dtos;
    }
    
    [HttpPut]
    [Route("{hashAsHex}/part/{index}")]
    public async Task<IActionResult> UploadFilePart(CancellationToken token, string hashAsHex, long index)
    {
        var user = User.FindFirstValue(ClaimTypes.Name);
        var definition = await ReadDefinition(hashAsHex);
        if (definition.Author != user)
            return Forbid("File Id does not match authorized user");
        _logger.Log(LogLevel.Information,
            $"Uploading File part {definition.OriginalFileName} - ({index} / {definition.Parts.Length})");
        
        var part = definition.Parts[index];

        await using var ms = new MemoryStream();
        await Request.Body.CopyToLimitAsync(ms, (int) part.Size, token);
        ms.Position = 0;
        if (ms.Length != part.Size)
            return BadRequest($"Couldn't read enough data for part {part.Size} vs {ms.Length}");

        var hash = await ms.Hash(token);
        if (hash != part.Hash)
            return BadRequest(
                $"Hashes don't match for index {index}. Sizes ({ms.Length} vs {part.Size}). Hashes ({hash} vs {part.Hash}");

        ms.Position = 0;
        await using var partStream = await CreatePart(hashAsHex, (int)index);
        await ms.CopyToAsync(partStream, token);
        return Ok(part.Hash.ToBase64());
    }

    [HttpPut]
    [Route("create/{hashAsHex}")]
    public async Task<IActionResult> CreateUpload(string hashAsHex)
    {
        var user = User.FindFirstValue(ClaimTypes.Name);

        var definition = (await _dtos.DeserializeAsync<FileDefinition>(Request.Body))!;

        _logger.Log(LogLevel.Information, "Creating File upload {Hash}", hashAsHex);

        definition.ServerAssignedUniqueId = hashAsHex;
        definition.Author = user;
        await WriteDefinition(definition);
        
        await _discord.Send(Channel.Ham,
            new DiscordMessage
            {
                Content =
                    $"{user} has started mirroring {definition.OriginalFileName} ({definition.Size.ToFileSizeString()})"
            });

        return Ok(definition.ServerAssignedUniqueId);
    }

    [HttpPut]
    [Route("{hashAsHex}/finish")]
    public async Task<IActionResult> FinishUpload(string hashAsHex)
    {
        var user = User.FindFirstValue(ClaimTypes.Name);
        var definition = await ReadDefinition(hashAsHex);
        if (definition.Author != user)
            return Forbid("File Id does not match authorized user");
        _logger.Log(LogLevel.Information, "Finalizing file upload {Hash}", hashAsHex);

        await _discord.Send(Channel.Ham,
            new DiscordMessage
            {
                Content =
                    $"{user} has finished uploading {definition.OriginalFileName} ({definition.Size.ToFileSizeString()})"
            });

        var host = _settings.TestMode ? "test-files" : "authored-files";
        return Ok($"https://{host}.wabbajack.org/{definition.MungedName}");
    }

    [HttpDelete]
    [Route("{hashAsHex}")]
    public async Task<IActionResult> DeleteMirror(string hashAsHex)
    {
        var user = User.FindFirstValue(ClaimTypes.Name);
        var definition = await ReadDefinition(hashAsHex);

        await _discord.Send(Channel.Ham,
            new DiscordMessage
            {
                Content =
                    $"{user} is deleting {hashAsHex}, {definition.Size.ToFileSizeString()} to be freed"
            });
        _logger.Log(LogLevel.Information, "Deleting upload {Hash}", hashAsHex);

        RootPath(hashAsHex).DeleteDirectory();
        return Ok();
    }

    [HttpGet]
    [AllowAnonymous]
    [Route("")]
    public async Task<IActionResult> MirroredFilesGet()
    {
        var files = await AllMirroredFiles();
        foreach (var file in files)
            file.Parts = Array.Empty<PartDefinition>();
        return Ok(_dtos.Serialize(files));
    }
    
    
    public IEnumerable<AbsolutePath> AllDefinitions => MirrorFilesLocation.EnumerateFiles("definition.json.gz");
    public async Task<FileDefinition[]> AllMirroredFiles()
    {
        var defs = new List<FileDefinition>();
        foreach (var file in AllDefinitions)
        {
            defs.Add(await ReadDefinition(file));
        }
        return defs.ToArray();
    }
    
    public async Task<FileDefinition> ReadDefinition(string hashAsHex)
    {
        return await ReadDefinition(RootPath(hashAsHex).Combine("definition.json.gz"));
    }
    
    private async Task<FileDefinition> ReadDefinition(AbsolutePath file)
    {
        var gz = new GZipStream(new MemoryStream(await file.ReadAllBytesAsync()), CompressionMode.Decompress);
        var definition = (await _dtos.DeserializeAsync<FileDefinition>(gz))!;
        return definition;
    }
    
    public async Task WriteDefinition(FileDefinition definition)
    {
        var path = RootPath(definition.Hash.ToHex()).Combine("definition.json.gz");
        path.Parent.CreateDirectory();
        path.Parent.Combine("parts").CreateDirectory();
        
        await using var ms = new MemoryStream();
        await using (var gz = new GZipStream(ms, CompressionLevel.Optimal, true))
        {
            await _dtos.Serialize(definition, gz);
        }

        await path.WriteAllBytesAsync(ms.ToArray());
    }

    public AbsolutePath RootPath(string hashAsHex)
    {
        // Make sure it's a true hash before splicing into the path
        return MirrorFilesLocation.Combine(Hash.FromHex(hashAsHex).ToHex());
    }

    
    [HttpGet]
    [AllowAnonymous]
    [Route("direct_link/{hashAsHex}")]
    public async Task DirectLink(string hashAsHex)
    {
        var definition = await ReadDefinition(hashAsHex);
        Response.Headers.ContentDisposition =
            new StringValues($"attachment; filename={definition.OriginalFileName}");
        Response.Headers.ContentType = new StringValues("application/octet-stream");
        foreach (var part in definition.Parts)
        {
            await using var partStream = await StreamForPart(hashAsHex, (int)part.Index);
            await partStream.CopyToAsync(Response.Body);
        }
    }
    
    public async Task<Stream> StreamForPart(string hashAsHex, int part)
    {
        return RootPath(hashAsHex).Combine("parts", part.ToString()).Open(FileMode.Open);
    }
    
    public async Task<Stream> CreatePart(string hashAsHex, int part)
    {
        return RootPath(hashAsHex).Combine("parts", part.ToString()).Open(FileMode.Create, FileAccess.Write, FileShare.None);
    }
}