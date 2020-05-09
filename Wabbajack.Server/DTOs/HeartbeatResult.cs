using System;
using Wabbajack.Common.Serialization.Json;

namespace Wabbajack.Server.DTOs
{
    [JsonName("HeartbeatResult")]
    public class HeartbeatResult
    {
        public TimeSpan Uptime { get; set; }
        public TimeSpan LastNexusUpdate { get; set; }
            
        public TimeSpan LastListValidation { get; set; }
    }
}
