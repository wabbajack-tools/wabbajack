using System.Text;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using FluentFTP.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using Wabbajack.BuildServer;
using Wabbajack.Downloaders;
using Wabbajack.Downloaders.Interfaces;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using Wabbajack.VFS;

namespace Wabbajack.Server.Controllers;

[ApiController]
[Route("/proxy")]
public class Proxy : ControllerBase
{
    private readonly ILogger<Proxy> _logger;
    private readonly DownloadDispatcher _dispatcher;
    private readonly TemporaryFileManager _tempFileManager;
    private readonly AppSettings _appSettings;
    private readonly FileHashCache _hashCache;
    private readonly IAmazonS3 _s3;
    private readonly string _bucket;
    
    private string _redirectUrl = "https://proxy.wabbajack.org/";
    private readonly IResource<DownloadDispatcher> _resource;

    public Proxy(ILogger<Proxy> logger, DownloadDispatcher dispatcher, TemporaryFileManager tempFileManager, 
        FileHashCache hashCache, AppSettings appSettings, IAmazonS3 s3, IResource<DownloadDispatcher> resource)
    {
        _logger = logger;
        _dispatcher = dispatcher;
        _tempFileManager = tempFileManager;
        _appSettings = appSettings;
        _hashCache = hashCache;
        _s3 = s3;
        _bucket = _appSettings.S3.ProxyFilesBucket;
        _resource = resource;
    }

    [HttpHead]
    public async Task<IActionResult> ProxyHead(CancellationToken token, [FromQuery] Uri uri, [FromQuery] string? name,
        [FromQuery] string? hash)
    {
        var cacheName = (await Encoding.UTF8.GetBytes(uri.ToString()).Hash()).ToHex();
        return new RedirectResult(_redirectUrl + cacheName);
    }

    [HttpGet]
    public async Task<IActionResult> ProxyGet(CancellationToken token, [FromQuery] Uri uri, [FromQuery] string? name, [FromQuery] string? hash)
    {

        Hash hashResult = default;
        var shouldMatch = hash != null ? Hash.FromHex(hash) : default;
        
        _logger.LogInformation("Got proxy request for {Uri}", uri);
        var state = _dispatcher.Parse(uri);
        var cacheName = (await Encoding.UTF8.GetBytes(uri.ToString()).Hash()).ToHex();
        var cacheFile = await GetCacheEntry(cacheName);

        if (state == null)
        {
            return BadRequest(new {Type = "Could not get state from Uri", Uri = uri.ToString()});
        }

        var archive = new Archive
        {
            Name = name ?? "",
            State = state,
            Hash = shouldMatch

        };

        var downloader = _dispatcher.Downloader(archive);
        if (downloader is not IProxyable)
        {
            return BadRequest(new {Type = "Downloader is not IProxyable", Downloader = downloader.GetType().FullName});
        }
        
        if (cacheFile != null && (DateTime.UtcNow - cacheFile.LastModified) > TimeSpan.FromHours(4))
        {
            try
            {
                var verify = await _dispatcher.Verify(archive, token);
                if (verify)
                    await TouchCacheEntry(cacheName);
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex, "When trying to verify cached file ({Hash}) {Url}", 
                    cacheFile.Hash, uri);
                await TouchCacheEntry(cacheName);
            }
        }
        
        if (cacheFile != null && (DateTime.Now - cacheFile.LastModified) > TimeSpan.FromHours(24))
        {
            try
            {
                await DeleteCacheEntry(cacheName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "When trying to delete expired file");
            }
        }


        var redirectUrl = _redirectUrl + cacheName + "?response-content-disposition=attachment;filename=" + (name ?? "unknown");
        if (cacheFile != null)
        {
            if (hash != default)
            {
                if (cacheFile.Hash != shouldMatch)
                    return BadRequest(new {Type = "Unmatching Hashes", Expected = shouldMatch.ToHex(), Found = hashResult.ToHex()});
            }
            return new RedirectResult(redirectUrl);
        }

        _logger.LogInformation("Downloading proxy request for {Uri}", uri);
        
        var tempFile = _tempFileManager.CreateFile(deleteOnDispose:false);

        var proxyDownloader = _dispatcher.Downloader(archive) as IProxyable;

        using var job = await _resource.Begin("Downloading file", 0, token);
        hashResult = await proxyDownloader!.Download(archive, tempFile.Path, job, token);
        
        
        if (hash != default && hashResult != shouldMatch)
        {
            if (tempFile.Path.FileExists())
                tempFile.Path.Delete();
            return NotFound();
        }
        
        await PutCacheEntry(tempFile.Path, cacheName, hashResult);

        _logger.LogInformation("Returning proxy request for {Uri}", uri);
        return new RedirectResult(redirectUrl);
    }

    private async Task<CacheStatus?> GetCacheEntry(string name)
    {
        GetObjectMetadataResponse info;
        try
        {
            info = await _s3.GetObjectMetadataAsync(new GetObjectMetadataRequest()
            {
                BucketName = _bucket,
                Key = name,
            });
        }
        catch (Exception _)
        {
            return null;
        }

        if (info.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
            return null;
        
        if (info.Metadata["WJ-Hash"] == null)
            return null;
        
        if (!Hash.TryGetFromHex(info.Metadata["WJ-Hash"], out var hash))
            return null;
        
        return new CacheStatus
        {
            LastModified = info.LastModified,
            Size = info.ContentLength,
            Hash = hash
        };
    }
    
    private async Task TouchCacheEntry(string name)
    {
        await _s3.CopyObjectAsync(new CopyObjectRequest()
        {
            SourceBucket = _bucket,
            DestinationBucket = _bucket,
            SourceKey = name,
            DestinationKey = name,
            MetadataDirective = S3MetadataDirective.REPLACE,
        });
    }
    
    private async Task PutCacheEntry(AbsolutePath path, string name, Hash hash)
    {
        var obj = new PutObjectRequest
        {
            BucketName = _bucket,
            Key = name,
            FilePath = path.ToString(),
            ContentType = "application/octet-stream",
            DisablePayloadSigning = true
        };
        obj.Metadata.Add("WJ-Hash", hash.ToHex());
        await _s3.PutObjectAsync(obj);
    }
    
    private async Task DeleteCacheEntry(string name)
    {
        await _s3.DeleteObjectAsync(new DeleteObjectRequest
        {
            BucketName = _bucket,
            Key = name
        });
    }

    record CacheStatus
    {
        public DateTime LastModified { get; init; }
        public long Size { get; init; }
        
        public Hash Hash { get; init; }
    }
}