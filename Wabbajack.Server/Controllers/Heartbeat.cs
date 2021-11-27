using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Wabbajack.Server;
using Wabbajack.Server.DTOs;
using Wabbajack.Server.Services;

namespace Wabbajack.BuildServer.Controllers;

[Route("/heartbeat")]
public class Heartbeat : ControllerBase
{
    private static readonly DateTime _startTime;

    private readonly GlobalInformation _globalInformation;
    static Heartbeat()
    {
        _startTime = DateTime.Now;
    }

    public Heartbeat(ILogger<Heartbeat> logger, GlobalInformation globalInformation,
        QuickSync quickSync)
    {
        _globalInformation = globalInformation;
    }

    [HttpGet]
    public async Task<IActionResult> GetHeartbeat()
    {
        return Ok(new HeartbeatResult
        {
            Uptime = DateTime.Now - _startTime,
        });
    }
}