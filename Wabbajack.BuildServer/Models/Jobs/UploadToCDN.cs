using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using FluentFTP;
using Wabbajack.BuildServer.Model.Models;
using Wabbajack.BuildServer.Models.JobQueue;
using Wabbajack.Common;
using Wabbajack.Common.Serialization.Json;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;
using File = System.IO.File;

namespace Wabbajack.BuildServer.Models.Jobs
{
    [JsonName("UploadToCDN")]
    public class UploadToCDN : AJobPayload
    {
        public override string Description => $"Push an uploaded file ({FileId}) to the CDN";
        
        public Guid FileId { get; set; }
        
        public override async Task<JobResult> Execute(SqlService sql, AppSettings settings)
        {
            int retries = 0;
            TOP:
            var file = await sql.UploadedFileById(FileId);

            if (settings.BunnyCDN_User == "TEST" && settings.BunnyCDN_Password == "TEST")
            {
                return JobResult.Success();
            }
            
            using (var client = new FtpClient("storage.bunnycdn.com"))
            {
                client.Credentials = new NetworkCredential(settings.BunnyCDN_User, settings.BunnyCDN_Password);
                await client.ConnectAsync();
                using (var stream = File.OpenRead(Path.Combine("public", "files", file.MungedName)))
                {
                    try
                    {
                        await client.UploadAsync(stream, file.MungedName, progress: new Progress((RelativePath)file.MungedName));
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
                
                await sql.EnqueueJob(new Job
                {
                    Priority = Job.JobPriority.High,
                    Payload = new IndexJob
                    {
                        Archive = new Archive(new HTTPDownloader.State(file.Uri))
                        {
                            Name = file.MungedName,
                            Size = file.Size,
                            Hash = file.Hash,
                        }
                    }
                });
            }
            return JobResult.Success();
        }

        protected override IEnumerable<object> PrimaryKey => new object[] {FileId};

        public class Progress : IProgress<FluentFTP.FtpProgress>
        {
            private RelativePath _name;
            private DateTime LastUpdate = DateTime.UnixEpoch;

            public Progress(RelativePath name)
            {
                _name = name;
            }
            public void Report(FtpProgress value)
            {
                if (DateTime.Now - LastUpdate <= TimeSpan.FromSeconds(5)) return;

                Utils.Log($"Uploading {_name} - {value.Progress}% {(int)((value.TransferSpeed + 1) / 1024 / 1024)} MB/sec ETA: {value.ETA}");
                LastUpdate = DateTime.Now;

            }
        }
    }
}
