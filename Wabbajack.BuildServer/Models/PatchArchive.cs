using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using FluentFTP;
using Wabbajack.BuildServer.Model.Models;
using Wabbajack.BuildServer.Models.JobQueue;
using Wabbajack.BuildServer.Models.Jobs;
using Wabbajack.Common;
using Wabbajack.Common.Serialization.Json;

namespace Wabbajack.BuildServer.Models
{
    [JsonName("PatchArchive")]
    public class PatchArchive : AJobPayload
    {
        public override string Description => "Create a archive update patch";
        public Hash Src { get; set; }
        public string DestPK { get; set; }
        public override async Task<JobResult> Execute(SqlService sql, AppSettings settings)
        {
            var srcPath = settings.PathForArchive(Src);
            var destHash = (await sql.DownloadStateByPrimaryKey(DestPK)).Hash;
            var destPath = settings.PathForArchive(destHash);
            
            if (Src == destHash)
                return JobResult.Success();

            Utils.Log($"Creating Patch ({Src} -> {DestPK})");
            var cdnPath = CdnPath(Src, destHash);
            cdnPath.Parent.CreateDirectory();
            
            if (cdnPath.Exists)
                return JobResult.Success();

            Utils.Log($"Calculating Patch ({Src} -> {DestPK})");
            await using var fs = cdnPath.Create();
            await using (var srcStream = srcPath.OpenRead())
            await using (var destStream = destPath.OpenRead())
            await using (var sigStream = cdnPath.WithExtension(Consts.OctoSig).Create())
            {
                OctoDiff.Create(destStream, srcStream, sigStream, fs);
            }
            fs.Position = 0;
            
            Utils.Log($"Uploading Patch ({Src} -> {DestPK})");

            int retries = 0;
            
            if (settings.BunnyCDN_User == "TEST" && settings.BunnyCDN_Password == "TEST")
            {
                return JobResult.Success();
            }
            
            TOP:
            using (var client = new FtpClient("storage.bunnycdn.com"))
            {
                client.Credentials = new NetworkCredential(settings.BunnyCDN_User, settings.BunnyCDN_Password);
                await client.ConnectAsync();
                try
                {
                    await client.UploadAsync(fs, $"updates/{Src.ToHex()}_{destHash.ToHex()}", progress: new UploadToCDN.Progress(cdnPath.FileName));
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

        protected override IEnumerable<object> PrimaryKey => new object[] {Src, DestPK};

        public static AbsolutePath CdnPath(Hash srcHash, Hash destHash)
        {
            return $"updates/{srcHash.ToHex()}_{destHash.ToHex()}".RelativeTo(AbsolutePath.EntryPoint);
        }
    }
}
