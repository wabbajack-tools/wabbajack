using System.Text;
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
using Wabbajack.Paths.IO;
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

    public Proxy(ILogger<Proxy> logger, DownloadDispatcher dispatcher, TemporaryFileManager tempFileManager, FileHashCache hashCache, AppSettings appSettings)
    {
        _logger = logger;
        _dispatcher = dispatcher;
        _tempFileManager = tempFileManager;
        _appSettings = appSettings;
        _hashCache = hashCache;
    }
    
    [HttpGet]
    public async Task<IActionResult> ProxyGet(CancellationToken token, [FromQuery] Uri uri, [FromQuery] string? name, [FromQuery] string? hash)
    {
        var shouldMatch = hash != null ? Hash.FromHex(hash) : default;
        
        _logger.LogInformation("Got proxy request for {Uri}", uri);
        var state = _dispatcher.Parse(uri);
        var cacheName = (await Encoding.UTF8.GetBytes(uri.ToString()).Hash()).ToHex();
        var cacheFile = _appSettings.ProxyPath.Combine(cacheName);

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
        
        if (cacheFile.FileExists() && (DateTime.Now - cacheFile.LastModified()) > TimeSpan.FromHours(4))
        {
            try
            {
                var verify = await _dispatcher.Verify(archive, token);
                if (verify)
                    cacheFile.Touch();
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex, "When trying to verify cached file ({Hash}) {Url}", cacheFile.FileName, uri);
                cacheFile.Touch();
            }
        }
        
        if (cacheFile.FileExists() && (DateTime.Now - cacheFile.LastModified()) > TimeSpan.FromHours(24))
        {
            try
            {
                cacheFile.Delete();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "When trying to delete expired file");
            }
        }


        if (cacheFile.FileExists())
        {
            if (hash != default)
            {
                var hashResult = await _hashCache.FileHashCachedAsync(cacheFile, token);
                if (hashResult != shouldMatch)
                    return BadRequest(new {Type = "Unmatching Hashes", Expected = shouldMatch.ToHex(), Found = hashResult.ToHex()});
            }
            var ret = new PhysicalFileResult(cacheFile.ToString(), "application/octet-stream");
            if (name != null)
                ret.FileDownloadName = name;
            return ret;
        }

        _logger.LogInformation("Downloading proxy request for {Uri}", uri);
        
        var tempFile = _tempFileManager.CreateFile(deleteOnDispose:false);

        var proxyDownloader = _dispatcher.Downloader(archive) as IProxyable;
        await using (var of = tempFile.Path.Open(FileMode.Create, FileAccess.Write, FileShare.None))
        {
            Response.StatusCode = 200;
            if (name != null)
            {
                Response.Headers.Add(HeaderNames.ContentDisposition, $"attachment; filename=\"{name}\"");
            }

            Response.Headers.Add( HeaderNames.ContentType, "application/octet-stream"  );
            
            var result = await proxyDownloader.DownloadStream(archive, async s => { 
                    return await s.HashingCopy(async m =>
                {
                    var strmA = of.WriteAsync(m, token);
                    await Response.Body.WriteAsync(m, token);
                    await Response.Body.FlushAsync(token);
                    await strmA;
                }, token); },
                token);
            
            
            if (hash != default && result != shouldMatch)
            {
                if (tempFile.Path.FileExists())
                    tempFile.Path.Delete();
            }
        }


        await tempFile.Path.MoveToAsync(cacheFile, true, token);

        _logger.LogInformation("Returning proxy request for {Uri} {Size}", uri, cacheFile.Size().FileSizeToString());
        return new EmptyResult();
    }
}