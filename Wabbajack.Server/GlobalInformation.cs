using System;

namespace Wabbajack.Server
{
    public class GlobalInformation
    {
        public TimeSpan NexusRSSPollRate = TimeSpan.FromMinutes(1);
        public TimeSpan NexusAPIPollRate = TimeSpan.FromHours(2);
        public DateTime LastNexusSyncUTC { get; set; }
        public TimeSpan TimeSinceLastNexusSync => DateTime.UtcNow - LastNexusSyncUTC;
    }
}
