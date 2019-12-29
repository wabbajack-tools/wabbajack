using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wabbajack.CacheServer.DTOs;

namespace Wabbajack.CacheServer.ServerConfig
{
    public class BuildServerConfig
    {
        public MongoConfig<Metric> Metrics { get; set; }
        public MongoConfig<ModListStatus> ListValidation { get; set; }
    }
}
