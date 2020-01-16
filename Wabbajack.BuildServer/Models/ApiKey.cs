using System.Collections.Generic;
using System.Threading.Tasks;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace Wabbajack.BuildServer.Models
{
    public class ApiKey
    {
        public string Id { get; set; }
        
        public string Key { get; set; }
        public string Owner { get; set; }
        
        public List<string> CanUploadLists { get; set; }
        public List<string> Roles { get; set; }

        public static async Task<ApiKey> Get(DBContext db, string key)
        {
            return await db.ApiKeys.AsQueryable().Where(k => k.Key == key).FirstOrDefaultAsync();
        }
    }
}
