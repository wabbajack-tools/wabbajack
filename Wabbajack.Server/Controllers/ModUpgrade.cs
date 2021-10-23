using System;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Wabbajack.Downloaders;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.DTOs.ServerResponses;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Server.DataLayer;
using Wabbajack.Server.DTOs;
using Wabbajack.Server.Services;
using Wabbajack.Server.TokenProviders;

namespace Wabbajack.BuildServer.Controllers;

[ApiController]
public class ModUpgrade : ControllerBase
{
    private readonly DownloadDispatcher _dispatcher;
    private readonly DTOSerializer _dtos;
    private readonly IFtpSiteCredentials _ftpSite;
    private readonly ILogger<ModUpgrade> _logger;
    private readonly QuickSync _quickSync;
    private readonly AppSettings _settings;
    private readonly SqlService _sql;

    public ModUpgrade(ILogger<ModUpgrade> logger, SqlService sql, DiscordWebHook discord, QuickSync quickSync,
        AppSettings settings, DTOSerializer dtos,
        DownloadDispatcher dispatcher, IFtpSiteCredentials ftp)
    {
        _logger = logger;
        _sql = sql;
        _settings = settings;
        _quickSync = quickSync;
        _ftpSite = ftp;
        _dtos = dtos;
        _dispatcher = dispatcher;
    }

    [HttpPost]
    [Authorize(Roles = "User")]
    [Route("/mod_upgrade")]
    public async Task<IActionResult> PostModUpgrade(CancellationToken token)
    {
        var isAuthor = User.Claims.Any(c => c.Type == ClaimTypes.Role && c.Value == "Author");
        var request = await _dtos.DeserializeAsync<ModUpgradeRequest>(Request.Body);
        if (!isAuthor)
        {
            var srcDownload = await _sql.GetArchiveDownload(request!.OldArchive.State.PrimaryKeyString,
                request.OldArchive.Hash, request.OldArchive.Size);
            var destDownload = await _sql.GetArchiveDownload(request.NewArchive.State.PrimaryKeyString,
                request.NewArchive.Hash, request.NewArchive.Size);

            if (srcDownload == default || destDownload == default ||
                await _sql.FindPatch(srcDownload.Id, destDownload.Id) == default)
            {
                if (!await _dispatcher.IsAllowed(request, token))
                {
                    _logger.Log(LogLevel.Information,
                        $"Upgrade requested from {request.OldArchive.Hash} to {request.NewArchive.Hash} rejected as upgrade is invalid");
                    return BadRequest("Invalid mod upgrade");
                }

                if (_settings.ValidateModUpgrades && !await _sql.HashIsInAModlist(request.OldArchive.Hash))
                {
                    _logger.Log(LogLevel.Information,
                        $"Upgrade requested from {request.OldArchive.Hash} to {request.NewArchive.Hash} rejected as src hash is not in a curated modlist");
                    return BadRequest("Hash is not in a recent modlist");
                }
            }
        }

        try
        {
            if (await _dispatcher.Verify(request!.OldArchive, token))
                //_logger.LogInformation(
                //    $"Refusing to upgrade ({request.OldArchive.State.PrimaryKeyString}), old archive is valid");
                return NotFound("File is Valid");
        }
        catch (Exception)
        {
            //_logger.LogInformation(
            //     $"Refusing to upgrade ({request.OldArchive.State.PrimaryKeyString}), due to upgrade failure");
            return NotFound("File is Valid");
        }

        var oldDownload = await _sql.GetOrEnqueueArchive(request.OldArchive);

        if (await _sql.IsNoPatch(oldDownload.Archive.Hash)) return BadRequest("File has NoPatch attached");

        var newDownload = await _sql.GetOrEnqueueArchive(request.NewArchive);

        var patch = await _sql.FindOrEnqueuePatch(oldDownload.Id, newDownload.Id);
        if (patch.Finished.HasValue)
        {
            if (patch.PatchSize != 0)
            {
                //_logger.Log(LogLevel.Information, $"Upgrade requested from {oldDownload.Archive.Hash} to {newDownload.Archive.Hash} patch Found");
                var host = (await _ftpSite.Get())[StorageSpace.Patches].Username == "wabbajacktest"
                    ? "test-files"
                    : "patches";
                await _sql.MarkPatchUsage(oldDownload.Id, newDownload.Id);
                return
                    Ok(
                        $"https://{host}.wabbajack.org/{request.OldArchive.Hash.ToHex()}_{request.NewArchive.Hash.ToHex()}");
            }
            //_logger.Log(LogLevel.Information, $"Upgrade requested from {oldDownload.Archive.Hash} to {newDownload.Archive.Hash} patch found but was failed");

            return NotFound("Patch creation failed");
        }

        if (!newDownload.DownloadFinished.HasValue)
            await _quickSync.Notify<ArchiveDownloader>();
        else
            await _quickSync.Notify<PatchBuilder>();

        //_logger.Log(LogLevel.Information, $"Upgrade requested from {oldDownload.Archive.Hash} to {newDownload.Archive.Hash} patch found is processing");
        // Still processing
        return Accepted();
    }

    [HttpGet]
    [Authorize(Roles = "User")]
    [Route("/mod_upgrade/find/{hashAsHex}")]
    public async Task<IActionResult> FindUpgrade(string hashAsHex)
    {
        var hash = Hash.FromHex(hashAsHex);

        var patches = await _sql.PatchesForSource(hash);
        return Ok(_dtos.Serialize(patches.Select(p => p.Dest.Archive).ToArray()));
    }

    [HttpGet]
    [Authorize(Roles = "Author")]
    [Route("/mod_upgrade/no_patch/{hashAsHex}/{rationaleAsHex}")]
    public async Task<IActionResult> PurgePatch(string hashAsHex, string rationaleAsHex)
    {
        var hash = Hash.FromHex(hashAsHex);
        var rationale = Encoding.UTF8.GetString(rationaleAsHex.FromHex());
        await _sql.PurgePatch(hash, rationale);
        return Ok("Purged");
    }

    [HttpGet]
    [Authorize(Roles = "User")]
    [Route("/mirror/{hashAsHex}")]
    public async Task<IActionResult> HaveHash(string hashAsHex)
    {
        var result = await _sql.HaveMirror(Hash.FromHex(hashAsHex));
        if (result) return Ok($"https://{(await _ftpSite.Get())[StorageSpace.Mirrors].Username}.b-cdn.net/{hashAsHex}");
        return NotFound("Not Mirrored");
    }
}