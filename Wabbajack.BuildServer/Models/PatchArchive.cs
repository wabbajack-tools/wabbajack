using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using FluentFTP;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Wabbajack.BuildServer.Model.Models;
using Wabbajack.BuildServer.Models.JobQueue;
using Wabbajack.BuildServer.Models.Jobs;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;
using File = Alphaleonis.Win32.Filesystem.File;

namespace Wabbajack.BuildServer.Models
{
    public class PatchArchive : AJobPayload
    {
        public override string Description => "Create a archive update patch";
        public string Src { get; set; }
        public string DestPK { get; set; }
        public override async Task<JobResult> Execute(DBContext db, SqlService sql, AppSettings settings)
        {
            var srcPath = settings.PathForArchive(Src);
            var destHash = (await db.DownloadStates.AsQueryable().Where(s => s.Key == DestPK).FirstOrDefaultAsync()).Hash;
            var destPath = settings.PathForArchive(destHash);
            
            if (Src == destHash)
                return JobResult.Success();

            Utils.Log($"Creating Patch ({Src} -> {DestPK})");
            var cdnPath = CdnPath(Src, destHash);
            
            if (File.Exists(cdnPath))
                return JobResult.Success();

            Utils.Log($"Calculating Patch ({Src} -> {DestPK})");
            await using var fs = File.Create(cdnPath);
            
            await using (var srcStream = File.OpenRead(srcPath))
            await using (var destStream = File.OpenRead(destPath))
            await using (var sigStream = File.Create(cdnPath + ".octo_sig"))
            {
                OctoDiff.Create(destStream, srcStream, sigStream, fs);
            }
            fs.Position = 0;
            
            Utils.Log($"Uploading Patch ({Src} -> {DestPK})");

            int retries = 0;
            TOP:
            using (var client = new FtpClient("storage.bunnycdn.com"))
            {
                client.Credentials = new NetworkCredential(settings.BunnyCDN_User, settings.BunnyCDN_Password);
                await client.ConnectAsync();
                try
                {
                    await client.UploadAsync(fs, $"updates/{Src.FromBase64().ToHex()}_{destHash.FromBase64().ToHex()}", progress: new UploadToCDN.Progress(cdnPath));
                }
                catch (Exception ex)
                {
                    if (retries > 10) throw;
                    Utils.Log(ex.ToString());
                    Utils.Log("Retrying FTP Upload");
                    retries++;
                    goto TOP;
                }
            }
            
            return JobResult.Success();
            
        }

        public static string CdnPath(string srcHash, string destHash)
        {
            return $"updates/{srcHash.FromBase64().ToHex()}_{destHash.FromBase64().ToHex()}";
        }
    }
}
