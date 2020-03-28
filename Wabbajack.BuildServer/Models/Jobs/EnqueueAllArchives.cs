using System;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using System.Linq;
using FluentFTP;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Wabbajack.BuildServer.Model.Models;
using Wabbajack.BuildServer.Models.JobQueue;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.ModListRegistry;

namespace Wabbajack.BuildServer.Models.Jobs
{
    public class EnqueueAllArchives : AJobPayload, IBackEndJob
    {
        public override string Description => "Add missing modlist archives to indexer";
        public override async Task<JobResult> Execute(DBContext db, SqlService sql, AppSettings settings)
        {
            Utils.Log("Starting ModList indexing");
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

            var modlistPath = Consts.ModListDownloadFolder.Combine(list.Links.MachineURL + Consts.ModListExtension);

            if (list.NeedsDownload(modlistPath))
            {
                modlistPath.Delete();

                var state = DownloadDispatcher.ResolveArchive(list.Links.Download);
                Utils.Log($"Downloading {list.Links.MachineURL} - {list.Title}");
                await state.Download(modlistPath);
            }
            else
            {
                Utils.Log($"No changes detected from downloaded ModList");
            }

            Utils.Log($"Loading {modlistPath}");

            var installer = AInstaller.LoadFromFile(modlistPath);

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

            Utils.Log($"Writing jobs to the database");
            await db.Jobs.InsertManyAsync(jobs, new InsertManyOptions {IsOrdered = false});
            Utils.Log($"Done adding archives for {installer.Name}");
        }
    }
}
