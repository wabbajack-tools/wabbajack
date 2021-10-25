using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Server.DataLayer;

namespace Wabbajack.BuildServer.Controllers;

[Authorize(Roles = "User")]
[ApiController]
[Route("/mod_files")]
public class ModFilesForHash : ControllerBase
{
    private readonly DTOSerializer _dtos;
    private ILogger<ModFilesForHash> _logger;
    private readonly SqlService _sql;

    public ModFilesForHash(ILogger<ModFilesForHash> logger, SqlService sql, DTOSerializer dtos)
    {
        _logger = logger;
        _sql = sql;
        _dtos = dtos;
    }

    [HttpGet("by_hash/{hashAsHex}")]
    public async Task<IActionResult> GetByHash(string hashAsHex)
    {
        var files = await _sql.ResolveDownloadStatesByHash(Hash.FromHex(hashAsHex));
        return Ok(_dtos.Serialize(files));
    }
}