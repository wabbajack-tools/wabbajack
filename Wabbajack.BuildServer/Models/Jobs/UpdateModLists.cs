using System;
using System.Linq;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Wabbajack.BuildServer.Models.JobQueue;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.ModListRegistry;
using Wabbajack.Lib.Validation;
using File = Alphaleonis.Win32.Filesystem.File;

namespace Wabbajack.BuildServer.Models.Jobs
{
    public class UpdateModLists : AJobPayload
    {
        public override string Description => "Validate curated modlists";
        public override async Task<JobResult> Execute(DBContext db, AppSettings settings)
        {
            Utils.Log("Starting Modlist Validation");
            var modlists = await ModlistMetadata.LoadFromGithub();

            using (var queue = new WorkQueue())
            {
                            
                var whitelists = new ValidateModlist(queue);
                await whitelists.LoadListsFromGithub();
                
                foreach (var list in modlists)
                {
                    try
                    {
                        await ValidateList(db, list, queue, whitelists);
                    }
                    catch (Exception ex)
                    {
                        Utils.Log(ex.ToString());
                    }
                }
            }

            return JobResult.Success();
        }
        
         private static async Task ValidateList(DBContext db, ModlistMetadata list, WorkQueue queue, ValidateModlist whitelists)
        {
            var existing = await db.ModListStatus.FindOneAsync(l => l.Id == list.Links.MachineURL);
            
            var modlist_path = Path.Combine(Consts.ModListDownloadFolder, list.Links.MachineURL + ExtensionManager.Extension);

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

            Utils.Log($"{installer.Archives.Count} archives to validate");

            DownloadDispatcher.PrepareAll(installer.Archives.Select(a => a.State));


            var validated = (await installer.Archives
                    .PMap(queue, async archive =>
                    {
                        bool is_failed;
                        try
                        {
                            is_failed = !(await archive.State.Verify(archive)) || !archive.State.IsWhitelisted(whitelists.ServerWhitelist);
                        }
                        catch (Exception)
                        {
                            is_failed = false;
                        }

                        return new DetailedStatusItem {IsFailing = is_failed, Archive = archive};
                    }))
                .ToList();


            var status = new DetailedStatus
            {
                Name = list.Title,
                Archives = validated.OrderBy(v => v.Archive.Name).ToList(),
                DownloadMetaData = list.DownloadMetadata,
                HasFailures = validated.Any(v => v.IsFailing)
            };

            var dto = new ModListStatus
            {
                Id = list.Links.MachineURL,
                Summary = new ModlistSummary
                {
                    Name = status.Name,
                    Checked = status.Checked,
                    Failed = status.Archives.Count(a => a.IsFailing),
                    Passed = status.Archives.Count(a => !a.IsFailing),
                },
                DetailedStatus = status,
                Metadata = list
            };
            Utils.Log(
                $"Writing Update for {dto.Summary.Name} - {dto.Summary.Failed} failed - {dto.Summary.Passed} passed");
            await ModListStatus.Update(db, dto);
            Utils.Log(
                $"Done updating {dto.Summary.Name}");

        }
    }
}
