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
using Wabbajack.VFS;

namespace Wabbajack.Server.Controllers;

[ApiController]
public class Proxy : ControllerBase
{
    private readonly ILogger<Proxy> _logger;
    private readonly DownloadDispatcher _dispatcher;
    private readonly AppSettings _appSettings;
    private readonly IAmazonS3 _s3;

    public Proxy(ILogger<Proxy> logger, DownloadDispatcher dispatcher, AppSettings appSettings, IAmazonS3 s3)
    {
        _logger = logger;
        _dispatcher = dispatcher;
        _appSettings = appSettings;
        _s3 = s3;
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

        var hash = await pDownloader.DownloadStream(archive, async s =>
        {
            var (inputStream, hashFn) = s.HashingPull();
            
            await _s3.PutObjectAsync(new PutObjectRequest
            {
                Key = tmpName,
                BucketName = _appSettings.ProxyStorage.BucketName,
                InputStream = inputStream,
                DisablePayloadSigning = true,
                ContentType = "application/octet-stream",
                Headers =
                {
                    ContentLength = s.Length,
                }
            }, token);
            return hashFn();
        }, token);

        JsonSerializer.Serialize(new Response()
        {
            Hash = hash.ToHex(),
            TempId = tmpName
        });
        
        return Ok();
    }

    public class Response
    {
        public string Hash { get; set; }
        public string TempId { get; set; }
    }
}