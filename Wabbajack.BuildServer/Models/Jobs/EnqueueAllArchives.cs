using System;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using System.Linq;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Wabbajack.BuildServer.Models.JobQueue;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.ModListRegistry;

namespace Wabbajack.BuildServer.Models.Jobs
{
    public class EnqueueAllArchives : AJobPayload
    {
        public override string Description => "Add missing modlist archives to indexer";
        public override async Task<JobResult> Execute(DBContext db, AppSettings settings)
        {
            Utils.Log("Starting modlist indexing");
            var modlists = await ModlistMetadata.LoadFromGithub();

            using (var queue = new WorkQueue())
            {
                foreach (var list in modlists)
                {
                    try
                    {
                        await EnqueueFromList(db, list, queue);
                    }
                    catch (Exception ex)
                    {
                        Utils.Log(ex.ToString());
                    }
                }
            }

            return JobResult.Success();
        }

        private static async Task EnqueueFromList(DBContext db, ModlistMetadata list, WorkQueue queue)
        {
            var existing = await db.ModListStatus.FindOneAsync(l => l.Id == list.Links.MachineURL);

            var modlist_path = Path.Combine(Consts.ModListDownloadFolder,
                list.Links.MachineURL + ExtensionManager.Extension);

            if (list.NeedsDownload(modlist_path))
            {
                if (File.Exists(modlist_path))
                    File.Delete(modlist_path);

                var state = DownloadDispatcher.ResolveArchive(list.Links.Download);
                Utils.Log($"Downloading {list.Links.MachineURL} - {list.Title}");
                await state.Download(modlist_path);
            }
            else
            {
                Utils.Log($"No changes detected from downloaded modlist");
            }

            Utils.Log($"Loading {modlist_path}");

            var installer = AInstaller.LoadFromFile(modlist_path);

            var archives = installer.Archives;

            Utils.Log($"Found {archives.Count} archives in {installer.Name} to index");
            var searching = archives.Select(a => a.Hash).Distinct().ToArray();

            Utils.Log($"Looking for missing archives");
            var knownArchives = (await db.IndexedFiles.AsQueryable().Where(a => searching.Contains(a.Hash))
                .Select(d => d.Hash).ToListAsync()).ToDictionary(a => a);

            Utils.Log($"Found {knownArchives.Count} pre-existing archives");
            var missing = archives.Where(a => !knownArchives.ContainsKey(a.Hash)).ToList();

            Utils.Log($"Found {missing.Count} missing archives, enqueing indexing jobs");

            var jobs = missing.Select(a => new Job {Payload = new IndexJob {Archive = a}, Priority = Job.JobPriority.Low});

            Utils.Log($"Writing jobs to the DB");
            await db.Jobs.InsertManyAsync(jobs, new InsertManyOptions {IsOrdered = false});
            Utils.Log($"Done adding archives for {installer.Name}");
        }
    }
}
