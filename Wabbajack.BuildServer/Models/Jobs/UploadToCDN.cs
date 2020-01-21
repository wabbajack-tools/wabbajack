using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using BunnyCDN.Net.Storage;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Wabbajack.BuildServer.Models.JobQueue;
using Wabbajack.Common;

namespace Wabbajack.BuildServer.Models.Jobs
{
    public class UploadToCDN : AJobPayload
    {
        public override string Description => $"Push an uploaded file ({FileId}) to the CDN";
        
        public string FileId { get; set; }
        
        public override async Task<JobResult> Execute(DBContext db, AppSettings settings)
        {
            var file = await db.UploadedFiles.AsQueryable().Where(f => f.Id == FileId).FirstOrDefaultAsync();
            var cdn = new BunnyCDNStorage(settings.BunnyCDNZone, settings.BunnyCDNApiKey);
            Utils.Log($"CDN Push {file.MungedName} to {settings.BunnyCDNZone}");
            await cdn.UploadAsync(Path.Combine("public", "files", file.MungedName), $"{settings.BunnyCDNZone}/{file.MungedName}");
            return JobResult.Success();
        }
    }
}
