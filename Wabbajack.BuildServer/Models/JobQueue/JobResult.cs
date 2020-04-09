using System;
using Wabbajack.Common.Serialization.Json;

namespace Wabbajack.BuildServer.Models.JobQueue
{
    [JsonName("JobResult")]
    public class JobResult
    {
        public JobResultType ResultType { get; set; }

        public string Message { get; set; }

        public string Stacktrace { get; set; }

        public static JobResult Success()
        {
            return new JobResult { ResultType = JobResultType.Success };
        }

        public static JobResult Error(Exception ex)
        {
            return new JobResult {ResultType = JobResultType.Error, Stacktrace = ex.ToString()};
        }

    }

    public enum JobResultType
    {
        Success,
        Error
    }
}
