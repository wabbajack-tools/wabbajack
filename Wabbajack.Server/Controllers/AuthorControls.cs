using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using FluentFTP.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Nettle;
using Wabbajack.Common;
using Wabbajack.DTOs;
using Wabbajack.DTOs.GitHub;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Networking.GitHub;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using Wabbajack.Server.DataModels;
using Wabbajack.Server.Extensions;
using Wabbajack.Server.Services;

namespace Wabbajack.BuildServer.Controllers;

[Authorize(Roles = "Author")]
[Route("/author_controls")]
public class AuthorControls : ControllerBase
{
    private readonly HttpClient _client;
    private readonly DTOSerializer _dtos;
    private readonly Client _gitHubClient;
    private readonly QuickSync _quickSync;
    private readonly AppSettings _settings;
    private readonly ILogger<AuthorControls> _logger;
    private readonly AuthorFiles _authorFiles;
    private readonly IResource<HttpClient> _limiter;

    public AuthorControls(ILogger<AuthorControls> logger, QuickSync quickSync, HttpClient client,
        AppSettings settings, DTOSerializer dtos, AuthorFiles authorFiles,
        Client gitHubClient, IResource<HttpClient> limiter)
    {
        _logger = logger;
        _quickSync = quickSync;
        _client = client;
        _settings = settings;
        _dtos = dtos;
        _gitHubClient = gitHubClient;
        _authorFiles = authorFiles;
        _limiter = limiter;
    }

    [Route("login/{authorKey}")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(string authorKey)
    {
        Response.Cookies.Append(ApiKeyAuthenticationHandler.ApiKeyHeaderName, authorKey);
        return Redirect($"{_settings.WabbajackBuildServerUri}author_controls/home");
    }

    [Route("lists")]
    [HttpGet]
    public async Task<IActionResult> AuthorLists()
    {
        var user = User.FindFirstValue(ClaimTypes.Name);
        var lists = (await LoadLists())
            .Where(l => l.Maintainers.Contains(user))
            .Select(l => l.NamespacedName)
            .ToArray();

        return Ok(lists);
    }
    
    public async Task<ModlistMetadata[]> LoadLists()
    {
        var repos = await LoadRepositories();

        return await repos.PMapAll(async url =>
            {
                try
                {
                    return (await _client.GetFromJsonAsync<ModlistMetadata[]>(_limiter,
                        new HttpRequestMessage(HttpMethod.Get, url.Value),
                        _dtos.Options))!.Select(meta =>
                    {
                        meta.RepositoryName = url.Key;
                        return meta;
                    });
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "While loading repository {Name} from {Url}", url.Key, url.Value);
                    return Enumerable.Empty<ModlistMetadata>();
                }
            })
            .SelectMany(x => x)
            .ToArray();
    }

    public async Task<Dictionary<string, Uri>> LoadRepositories()
    {
        var repositories = await _client.GetFromJsonAsync<Dictionary<string, Uri>>(_limiter,
            new HttpRequestMessage(HttpMethod.Get,
                "https://raw.githubusercontent.com/wabbajack-tools/mod-lists/master/repositories.json"), _dtos.Options);
        return repositories!;
    }

    [Route("whoami")]
    [HttpGet]
    public async Task<IActionResult> GetWhoAmI()
    {
        var user = User.FindFirstValue(ClaimTypes.Name);
        return Ok(user!);
    }


    [Route("lists/download_metadata")]
    [HttpPost]
    public async Task<IActionResult> PostDownloadMetadata()
    {
        var user = User.FindFirstValue(ClaimTypes.Name);
        var data = await _dtos.DeserializeAsync<UpdateRequest>(Request.Body);
        try
        {
            await _gitHubClient.UpdateList(user, data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "During posting of download_metadata");
            return BadRequest(ex);
        }

        return Ok(data);
    }

    private static async Task<string> HomePageTemplate(object o)
    {
        var data = await KnownFolders.EntryPoint.Combine(@"Controllers\Templates\AuthorControls.html")
            .ReadAllTextAsync();
        var func = NettleEngine.GetCompiler().Compile(data);
        return func(o);
    }

    [Route("home")]
    [Authorize("")]
    public async Task<IActionResult> HomePage()
    {
        var user = User.FindFirstValue(ClaimTypes.Name);
        var files = _authorFiles.AllDefinitions
            .Where(af => af.Definition.Author == user)
            .Select(af => new
            {
                Size = af.Definition.Size.FileSizeToString(),
                OriginalSize = af.Definition.Size,
                Name = af.Definition.OriginalFileName,
                MangledName = af.Definition.MungedName,
                UploadedDate = af.Updated
            })
            .OrderBy(f => f.Name)
            .ThenBy(f => f.UploadedDate)
            .ToList();

        var result = HomePageTemplate(new
        {
            User = user,
            TotalUsage = files.Select(f => f.OriginalSize).Sum().ToFileSizeString(),
            WabbajackFiles = files.Where(f => f.Name.EndsWith(Ext.Wabbajack.ToString())),
            OtherFiles = files.Where(f => !f.Name.EndsWith(Ext.Wabbajack.ToString()))
        });

        return new ContentResult
        {
            ContentType = "text/html",
            StatusCode = (int) HttpStatusCode.OK,
            Content = await result
        };
    }
}