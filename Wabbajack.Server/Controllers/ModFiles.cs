using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Wabbajack.DTOs;
using Wabbajack.DTOs.JsonConverters;

namespace Wabbajack.BuildServer.Controllers;

[Authorize(Roles = "User")]
[ApiController]
[Route("/mod_files")]
public class ModFilesForHash : ControllerBase
{
    private readonly DTOSerializer _dtos;
    private ILogger<ModFilesForHash> _logger;

    public ModFilesForHash(ILogger<ModFilesForHash> logger, DTOSerializer dtos)
    {
        _logger = logger;
        _dtos = dtos;
    }

    [HttpGet("by_hash/{hashAsHex}")]
    public async Task<IActionResult> GetByHash(string hashAsHex)
    {
        var empty = Array.Empty<Archive>();
        return Ok(_dtos.Serialize(empty));
    }
}