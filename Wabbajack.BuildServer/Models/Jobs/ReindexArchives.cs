using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Wabbajack.BuildServer.Model.Models;
using Wabbajack.BuildServer.Models.JobQueue;
using Wabbajack.Common;
using Wabbajack.VirtualFileSystem;

namespace Wabbajack.BuildServer.Models.Jobs
{
    public class ReindexArchives : AJobPayload
    {
        public override string Description => "Reindex all files in the mod archive folder";
        public override async Task<JobResult> Execute(DBContext db, SqlService sql, AppSettings settings)
        {
            using (var queue = new WorkQueue())
            {
                var files = Directory.EnumerateFiles(settings.ArchiveDir)
                    .Where(f => !f.EndsWith(Consts.HashFileExtension))
                    .ToList();
                var total_count = files.Count;
                int completed = 0;

                
                await files.PMap(queue, async file =>
                    {
                        try
                        {
                            Interlocked.Increment(ref completed);

                            if (await sql.HaveIndexdFile(await file.FileHashCachedAsync()))
                            {
                                Utils.Log($"({completed}/{total_count}) Skipping {Path.GetFileName(file)}, it's already indexed");
                                return;
                            }

                            var sub_folder = Guid.NewGuid().ToString();
                            string folder = Path.Combine(settings.DownloadDir, sub_folder);
                            
                            Utils.Log($"({completed}/{total_count}) Copying {file}");
                            Directory.CreateDirectory(folder);

                            Utils.Log($"({completed}/{total_count}) Copying {file}");
                            File.Copy(file, Path.Combine(folder, Path.GetFileName(file)));

                            Utils.Log($"({completed}/{total_count}) Analyzing {file}");
                            var vfs = new Context(queue, true);
                            await vfs.AddRoot(folder);

                            var root = vfs.Index.ByRootPath.First().Value;

                            Utils.Log($"({completed}/{total_count}) Ingesting {root.ThisAndAllChildren.Count()} files");

                            await sql.MergeVirtualFile(root);
                            Utils.Log($"({completed}/{total_count}) Cleaning up {file}");
                            await Utils.DeleteDirectory((AbsolutePath)folder);
                        }
                        catch (Exception ex)
                        {
                            Utils.Log(ex.ToString());
                        }

                    });
            }
            return JobResult.Success();
        }
    }
}
