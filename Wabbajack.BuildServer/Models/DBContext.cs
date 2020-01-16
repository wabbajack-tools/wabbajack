using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using Wabbajack.Lib.NexusApi;
using Wabbajack.BuildServer.Models.JobQueue;

namespace Wabbajack.BuildServer.Models
{
    public class DBContext
    {
        private IConfiguration _configuration;
        private Settings _settings;
        public DBContext(IConfiguration configuration)
        {
            _configuration = configuration;

            _settings = new Settings();
            _configuration.Bind("MongoDB", _settings);
        }
        
        public IMongoCollection<NexusCacheData<ModInfo>> NexusModInfos => Client.GetCollection<NexusCacheData<ModInfo>>(_settings.Collections["NexusModInfos"]);
        public IMongoCollection<NexusCacheData<NexusFileInfo>> NexusFileInfos => Client.GetCollection<NexusCacheData<NexusFileInfo>>(_settings.Collections["NexusFileInfos"]);
        public IMongoCollection<ModListStatus> ModListStatus => Client.GetCollection<ModListStatus>(_settings.Collections["ModListStatus"]);
        
        public IMongoCollection<Job> Jobs => Client.GetCollection<Job>(_settings.Collections["JobQueue"]);
        public IMongoCollection<DownloadState> DownloadStates => Client.GetCollection<DownloadState>(_settings.Collections["DownloadStates"]);
        public IMongoCollection<Metric> Metrics => Client.GetCollection<Metric>(_settings.Collections["Metrics"]);
        public IMongoCollection<IndexedFile> IndexedFiles => Client.GetCollection<IndexedFile>(_settings.Collections["IndexedFiles"]);
        public IMongoCollection<NexusCacheData<List<NexusUpdateEntry>>> NexusUpdates => Client.GetCollection<NexusCacheData<List<NexusUpdateEntry>>>(_settings.Collections["NexusUpdates"]);
        
        public IMongoCollection<ApiKey> ApiKeys => Client.GetCollection<ApiKey>(_settings.Collections["ApiKeys"]);        
        public IMongoCollection<UploadedFile> UploadedFiles => Client.GetCollection<UploadedFile>(_settings.Collections["UploadedFiles"]);

        public IMongoCollection<NexusCacheData<NexusApiClient.GetModFilesResponse>> NexusModFiles =>
            Client.GetCollection<NexusCacheData<NexusApiClient.GetModFilesResponse>>(
                _settings.Collections["NexusModFiles"]);
        private IMongoDatabase Client => new MongoClient($"mongodb://{_settings.Host}").GetDatabase(_settings.Database);
    }
    public class Settings
    {
        public string Host { get; set; }
        public string Database { get; set; }
        public Dictionary<string, string> Collections { get; set; }
    }
}
