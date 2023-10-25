using System.Text;
using System.Text.Json;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using Wabbajack.BuildServer;
using Wabbajack.Downloaders;
using Wabbajack.Downloaders.Interfaces;
using Wabbajack.DTOs;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using Wabbajack.VFS;

namespace Wabbajack.Server.Controllers;

[ApiController]
public class Proxy : ControllerBase
{
    private readonly ILogger<Proxy> _logger;
    private readonly DownloadDispatcher _dispatcher;
    private readonly AppSettings _appSettings;
    private readonly IAmazonS3 _s3;
    private readonly TemporaryFileManager _temporaryFileManager;

    public Proxy(ILogger<Proxy> logger, DownloadDispatcher dispatcher, AppSettings appSettings, IAmazonS3 s3, TemporaryFileManager temporaryFileManager)
    {
        _logger = logger;
        _dispatcher = dispatcher;
        _appSettings = appSettings;
        _s3 = s3;
        _dispatcher.UseProxy = false;
        _temporaryFileManager = temporaryFileManager;
    }
    
    [HttpGet("/verify")]
    public async Task<IActionResult> ProxyValidate(CancellationToken token, [FromQuery] Uri uri, [FromQuery] string hashAsHex)
    {
        _logger.LogInformation("Got proxy request for {Uri}", uri);
        var state = _dispatcher.Parse(uri);
        
        if (state == null)
        {
            return BadRequest(new {Type = "Could not get state from Uri", Uri = uri.ToString()});
        }

        var archive = new Archive
        {
            State = state,
            Hash = Hash.FromHex(hashAsHex)
        };

        var downloader = _dispatcher.Downloader(archive);
        if (downloader is not IProxyable pDownloader)
        {
            return BadRequest(new {Type = "Downloader is not IProxyable", Downloader = downloader.GetType().FullName});
        }
        
        var isValid = await _dispatcher.Verify(archive, token);
        
        return isValid ? Ok() : BadRequest();
    }
    
    [HttpGet("/proxy")]
    public async Task<IActionResult> ProxyGet(CancellationToken token, [FromQuery] Uri uri)
    {
        _logger.LogInformation("Got proxy request for {Uri}", uri);
        var state = _dispatcher.Parse(uri);
        
        if (state == null)
        {
            return BadRequest(new {Type = "Could not get state from Uri", Uri = uri.ToString()});
        }

        var archive = new Archive
        {
            State = state,
        };

        var downloader = _dispatcher.Downloader(archive);
        if (downloader is not IProxyable pDownloader)
        {
            return BadRequest(new {Type = "Downloader is not IProxyable", Downloader = downloader.GetType().FullName});
        }

        var tmpName = Guid.NewGuid().ToString();
        
        await using var file = _temporaryFileManager.CreateFile();

        var hash = await pDownloader.DownloadStream(archive, async s =>
        {
            await using var outFs = file.Path.Open(FileMode.Create, FileAccess.Write);
            return await s.HashingCopy(outFs, token);
        }, token);
        
        await _s3.PutObjectAsync(new PutObjectRequest()
        {
            Key = tmpName,
            BucketName = _appSettings.ProxyStorage.BucketName,
            DisablePayloadSigning = true,
            FilePath = file.ToString(),
            ContentType = "application/octet-stream",
        }, token);
        
        var data = JsonSerializer.Serialize(new DataResponse
        {
            Hash = hash.ToHex(),
            TempId = tmpName
        });
        
        return Ok(data);
    }

    public class DataResponse
    {
        public string Hash { get; set; }
        public string TempId { get; set; }
    }
}