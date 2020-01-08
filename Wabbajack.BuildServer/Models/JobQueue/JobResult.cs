using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson.Serialization.Attributes;

namespace Wabbajack.BuildServer.Models.JobQueue
{
    public class JobResult
    {
        public JobResultType ResultType { get; set; }
        [BsonIgnoreIfNull]
        public string Message { get; set; }

        [BsonIgnoreIfNull]
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
