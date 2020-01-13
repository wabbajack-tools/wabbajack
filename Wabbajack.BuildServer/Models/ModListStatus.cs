using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Wabbajack.Lib;
using Wabbajack.Lib.ModListRegistry;

namespace Wabbajack.BuildServer.Models
{
    public class ModListStatus
    {

        [BsonId]
        public string Id { get; set; }
        public ModlistSummary Summary { get; set; }

        public ModlistMetadata Metadata { get; set; }
        public DetailedStatus DetailedStatus { get; set; }

        public static async Task Update(DBContext db, ModListStatus status)
        {
            var id = status.Metadata.Links.MachineURL;
            await db.ModListStatus.FindOneAndReplaceAsync<ModListStatus>(s => s.Id == id, status, new FindOneAndReplaceOptions<ModListStatus> {IsUpsert = true});
        }

        public static IQueryable<ModListStatus> AllSummaries
        {
            get
            {
                return null;
            }
        }

        public static async Task<ModListStatus> ByName(DBContext db, string name)
        {
            var result = await db.ModListStatus
                .AsQueryable()
                .Where(doc => doc.Metadata.Links.MachineURL == name || doc.Metadata.Title == name)
                .ToListAsync();
            return result.First();
        }
    }

    public class DetailedStatus
    {
        public string Name { get; set; }
        public DateTime Checked { get; set; } = DateTime.UtcNow;
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
