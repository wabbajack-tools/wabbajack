using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentFTP.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Nettle;
using Wabbajack.Common;
using Wabbajack.DTOs.GitHub;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Networking.GitHub;
using Wabbajack.Paths.IO;
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

    public AuthorControls(ILogger<AuthorControls> logger, QuickSync quickSync, HttpClient client,
        AppSettings settings, DTOSerializer dtos, AuthorFiles authorFiles,
        Client gitHubClient)
    {
        _logger = logger;
        _quickSync = quickSync;
        _client = client;
        _settings = settings;
        _dtos = dtos;
        _gitHubClient = gitHubClient;
        _authorFiles = authorFiles;
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
        List<string> lists = new();
        foreach (var file in Enum.GetValues<List>())
            lists.AddRange((await _gitHubClient.GetData(file)).Lists.Where(l => l.Maintainers.Contains(user))
                .Select(lst => lst.NamespacedName));

        return Ok(lists);
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
        var files = (await _authorFiles.AllAuthoredFiles())
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