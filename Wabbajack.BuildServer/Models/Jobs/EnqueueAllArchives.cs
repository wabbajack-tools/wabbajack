using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Wabbajack.BuildServer.Model.Models;
using Wabbajack.BuildServer.Models.JobQueue;
using Wabbajack.Common;
using Wabbajack.Common.Serialization.Json;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.ModListRegistry;

namespace Wabbajack.BuildServer.Models.Jobs
{
    [JsonName("EnqueueAllArchives")]
    public class EnqueueAllArchives : AJobPayload, IBackEndJob
    {
        public override string Description => "Add missing modlist archives to indexer";
        public override async Task<JobResult> Execute(SqlService sql, AppSettings settings)
        {
            Utils.Log("Starting ModList indexing");
            var modlists = await ModlistMetadata.LoadFromGithub();

            using (var queue = new WorkQueue())
            {
                foreach (var list in modlists)
                {
                    try
                    {
                        await EnqueueFromList(sql, list, queue);
                    }
                    catch (Exception ex)
                    {
                        Utils.Log(ex.ToString());
                    }
                }
            }

            return JobResult.Success();
        }

        protected override IEnumerable<object> PrimaryKey => new object[0];

        private static async Task EnqueueFromList(SqlService sql, ModlistMetadata list, WorkQueue queue)
        {
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
            var searching = archives.Select(a => a.Hash).ToHashSet();

            Utils.Log($"Looking for missing archives");
            var knownArchives = await sql.FilterByExistingIndexedArchives(searching);

            Utils.Log($"Found {knownArchives.Count} pre-existing archives");
            var missing = archives.Where(a => !knownArchives.Contains(a.Hash)).ToList();

            Utils.Log($"Found {missing.Count} missing archives, enqueing indexing jobs");

            var jobs = missing.Select(a => new Job {Payload = new IndexJob {Archive = a}, Priority = Job.JobPriority.Low});

            Utils.Log($"Writing jobs to the database");
            
            foreach (var job in jobs)
                await sql.EnqueueJob(job);

            Utils.Log($"Done adding archives for {installer.Name}");
        }
    }
}
