using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Wabbajack.BuildServer.Model.Models;
using Wabbajack.BuildServer.Models.JobQueue;
using Wabbajack.Common;
using Wabbajack.Common.Serialization.Json;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;
using Wabbajack.VirtualFileSystem;

namespace Wabbajack.BuildServer.Models.Jobs
{ 
    
    [JsonName("IndexJob")]
    public class IndexJob : AJobPayload, IBackEndJob
    {
        public Archive Archive { get; set; }
        public override string Description => $"Index ${Archive.State.PrimaryKeyString} and save the download/file state";
        public override bool UsesNexus { get => Archive.State is NexusDownloader.State; }
        public override async Task<JobResult> Execute(SqlService sql, AppSettings settings)
        {
            if (Archive.State is ManualDownloader.State)
                return JobResult.Success();
            
            var pk = new List<object>();
            pk.Add(AbstractDownloadState.TypeToName[Archive.State.GetType()]);
            pk.AddRange(Archive.State.PrimaryKey);
            var pk_str = string.Join("|",pk.Select(p => p.ToString()));

            var found = await sql.DownloadStateByPrimaryKey(pk_str);
            if (found == null)
                return JobResult.Success();
            
            string fileName = Archive.Name;
            string folder = Guid.NewGuid().ToString();
            Utils.Log($"Indexer is downloading {fileName}");
            var downloadDest = settings.DownloadDir.Combine(folder, fileName);
            await Archive.State.Download(downloadDest);

            using (var queue = new WorkQueue())
            {
                var vfs = new Context(queue, true);
                await vfs.AddRoot(settings.DownloadDir.Combine(folder));
                var archive = vfs.Index.ByRootPath.First().Value;

                await sql.MergeVirtualFile(archive);

                await sql.AddDownloadState(archive.Hash, Archive.State);
                
                var to_path = settings.ArchiveDir.Combine(
                    $"{Path.GetFileName(fileName)}_{archive.Hash.ToHex()}_{Path.GetExtension(fileName)}");
                
                if (to_path.Exists)
                    downloadDest.Delete();
                else
                    downloadDest.MoveTo(to_path);
                await settings.DownloadDir.Combine(folder).DeleteDirectory();
                
            }
            return JobResult.Success();
        }

    }
    
}
