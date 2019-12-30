using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Wabbajack.Lib;
using Wabbajack.Lib.ModListRegistry;

namespace Wabbajack.CacheServer.DTOs
{
    public class ModListStatus
    {

        [BsonId]
        public string Id { get; set; }
        public ModlistSummary Summary { get; set; }

        public ModlistMetadata Metadata { get; set; }
        public DetailedStatus DetailedStatus { get; set; }

        public static async Task Update(ModListStatus status)
        {
            var id = status.Metadata.Links.MachineURL;
            await Server.Config.ListValidation.Connect().FindOneAndReplaceAsync<ModListStatus>(s => s.Id == id, status, new FindOneAndReplaceOptions<ModListStatus> {IsUpsert = true});
        }

        public static IQueryable<ModListStatus> AllSummaries
        {
            get
            {
                return null;
            }
        }

        public static async Task<ModListStatus> ByName(string name)
        {
            var result = await Server.Config.ListValidation.Connect()
                .AsQueryable()
                .Where(doc => doc.Metadata.Links.MachineURL == name || doc.Metadata.Title == name)
                .ToListAsync();
            return result.First();
        }

        public static IMongoQueryable<ModListStatus> All
        {
            get
            {
                return Server.Config.ListValidation.Connect().AsQueryable();
            }
        }
    }

    public class DetailedStatus
    {
        public string Name { get; set; }
        public DateTime Checked { get; set; } = DateTime.Now;
        public List<DetailedStatusItem> Archives { get; set; }
        public DownloadMetadata DownloadMetaData { get; set; }
        public bool HasFailures { get; set; }
        public string MachineName { get; set; }
    }

    public class DetailedStatusItem
    {
        public bool IsFailing { get; set; }
        public Archive Archive { get; set; }
    }
}
