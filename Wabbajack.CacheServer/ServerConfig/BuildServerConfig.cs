using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Driver.Core.Configuration;
using Wabbajack.CacheServer.DTOs;
using Wabbajack.CacheServer.DTOs.JobQueue;
using Wabbajack.Lib.NexusApi;

namespace Wabbajack.CacheServer.ServerConfig
{
    public class BuildServerConfig
    {
        public MongoConfig<Metric> Metrics { get; set; }
        public MongoConfig<ModListStatus> ListValidation { get; set; }

        public MongoConfig<Job> JobQueue { get; set; }

        public MongoConfig<IndexedFile> IndexedFiles { get; set; }
        public MongoConfig<DownloadState> DownloadStates { get; set; }

        public MongoConfig<NexusCacheData<ModInfo>> NexusModInfos { get; set; }
        public MongoConfig<NexusCacheData<NexusApiClient.GetModFilesResponse>> NexusModFiles { get; set; }
        public MongoConfig<NexusCacheData<NexusFileInfo>> NexusFileInfos { get; set; }

        public IndexerConfig Indexer { get; set; }

        public Settings Settings { get; set; }
    }
}
