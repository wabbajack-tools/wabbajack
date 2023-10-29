
using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text.Json;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Wabbajack.BuildServer;
using Wabbajack.Downloaders;
using Wabbajack.Downloaders.Interfaces;
using Wabbajack.DTOs;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;

namespace Wabbajack.Server.Controllers;

[ApiController]
public class Proxy : ControllerBase
{
    private readonly ILogger<Proxy> _logger;
    private readonly DownloadDispatcher _dispatcher;
    private readonly AppSettings _appSettings;
    private readonly IAmazonS3 _s3;
    private readonly TemporaryFileManager _temporaryFileManager;
    private readonly IResource<Proxy> _resource;
    private static readonly ConcurrentDictionary<Guid, Hash> _concurrentDictionary = new();

    public Proxy(ILogger<Proxy> logger, DownloadDispatcher dispatcher, AppSettings appSettings, IAmazonS3 s3, TemporaryFileManager temporaryFileManager, IResource<Proxy> resource)
    {
        _logger = logger;
        _dispatcher = dispatcher;
        _appSettings = appSettings;
        _s3 = s3;
        _dispatcher.UseProxy = false;
        _temporaryFileManager = temporaryFileManager;
        _resource = resource;
    }
    
    [HttpGet("/verify")]
    public async Task<IActionResult> ProxyValidate(CancellationToken token, [FromQuery] Uri uri, [FromQuery] string hashAsHex)
    {
        using var _ = await _resource.Begin($"Proxy Validate {uri}", 0, token);
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
        using var _ = await _resource.Begin($"Proxy Begin {uri}", 0, token);
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
    
    [HttpGet("/proxy_stream")]
    public async Task ProxyStream(CancellationToken token, [FromQuery] Uri uri)
    {
        using var _ = await _resource.Begin($"Proxy Begin {uri}", 0, token);
        _logger.LogInformation("Got proxy request for {Uri}", uri);
        var state = _dispatcher.Parse(uri);
        
        if (state == null)
        {
            Response.StatusCode = 400;
            await Response.WriteAsync("Could not get state from Uri", cancellationToken: token);
            return;
        }

        var archive = new Archive
        {
            State = state,
        };

        var downloader = _dispatcher.Downloader(archive);
        if (downloader is not IProxyable pDownloader)
        {
            Response.StatusCode = 400;
            await Response.WriteAsync("Downloader is not IProxyable", cancellationToken: token);
            return;
        }

        var id = Guid.NewGuid();
        var hash = await pDownloader.DownloadStream(archive, async s =>
        {
            _logger.LogInformation("Starting proxy stream for {Uri}", uri);
            Response.StatusCode = 200;
            Response.Headers.Add("Content-Type", "application/octet-stream");
            Response.Headers.Add("x-guid-id", id.ToString());
            Response.Headers.ContentLength = s.Length;
            await Response.Body.FlushAsync(token);
            return await s.HashingCopy(Response.Body, token, buffserSize: 1024);
        }, token);
        
        _concurrentDictionary.TryAdd(id, hash);
    }

    [HttpGet("/proxy_stream/{id}")]
    public async Task<IActionResult> ProxyStream(CancellationToken token, string id)
    {
        return Ok(_concurrentDictionary[Guid.Parse(id)].ToHex());
    }

    public class DataResponse
    {
        public string Hash { get; set; }
        public string TempId { get; set; }
    }
}