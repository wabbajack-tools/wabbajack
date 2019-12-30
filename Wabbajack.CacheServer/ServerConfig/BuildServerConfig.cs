using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wabbajack.CacheServer.DTOs;
using Wabbajack.CacheServer.DTOs.JobQueue;

namespace Wabbajack.CacheServer.ServerConfig
{
    public class BuildServerConfig
    {
        public MongoConfig<Metric> Metrics { get; set; }
        public MongoConfig<ModListStatus> ListValidation { get; set; }

        public MongoConfig<Job> JobQueue { get; set; }
    }
}
