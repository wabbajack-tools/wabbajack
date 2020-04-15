using System;
using System.Threading.Tasks;
using Wabbajack.Common.Serialization.Json;

namespace Wabbajack.BuildServer.Models.JobQueue
{ 
    [JsonName("Job")]
    public class Job
    {
        public enum JobPriority : int
        {
            Low,
            Normal,
            High,
        }

        public long Id { get; set; }
        public DateTime? Started { get; set; }
        public DateTime? Ended { get; set; }
        public DateTime Created { get; set; } = DateTime.Now;
        public JobPriority Priority { get; set; } = JobPriority.Normal;
        public JobResult Result { get; set; }
        public bool RequiresNexus { get; set; } = true;
        public AJobPayload Payload { get; set; }
        
        public Job OnSuccess { get; set; }
    }
}
