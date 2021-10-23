using System;

namespace Wabbajack.Server.DTOs;

public class HeartbeatResult
{
    public TimeSpan Uptime { get; set; }
    public TimeSpan LastNexusUpdate { get; set; }

    public TimeSpan LastListValidation { get; set; }
}