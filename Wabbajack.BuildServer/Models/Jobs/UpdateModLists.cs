using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Wabbajack.BuildServer.Model.Models;
using Wabbajack.BuildServer.Models.JobQueue;
using Wabbajack.Common;
using Wabbajack.Common.Serialization.Json;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.ModListRegistry;
using Wabbajack.Lib.NexusApi;
using Wabbajack.Lib.Validation;

namespace Wabbajack.BuildServer.Models.Jobs
{
    [JsonName("UpdateModLists")]
    public class UpdateModLists : AJobPayload, IFrontEndJob
    {
        public override string Description => "Validate curated modlists";
        public override async Task<JobResult> Execute(SqlService sql, AppSettings settings)
        {
            Utils.Log("Starting Modlist Validation");
            var modlists = await ModlistMetadata.LoadFromGithub();

            using (var queue = new WorkQueue())
            {
                            
                var whitelists = new ValidateModlist();
                await whitelists.LoadListsFromGithub();
                
                foreach (var list in modlists)
                {
                    try
                    {
                        await ValidateList(sql, list, queue, whitelists);
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

        private async Task ValidateList(SqlService sql, ModlistMetadata list, WorkQueue queue, ValidateModlist whitelists)
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
                Utils.Log($"No changes detected from downloaded modlist");
            }


            Utils.Log($"Loading {modlistPath}");

            var installer = AInstaller.LoadFromFile(modlistPath);

            Utils.Log($"{installer.Archives.Count} archives to validate");

            await DownloadDispatcher.PrepareAll(installer.Archives.Select(a => a.State));


            var validated = (await installer.Archives
                    .PMap(queue, async archive =>
                    {
                        var isValid = await IsValid(sql, whitelists, archive);

                        return new DetailedStatusItem {IsFailing = !isValid, Archive = archive};
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
                Summary = new ModListSummary
                {
                    Name = status.Name,
                    MachineURL = list.Links?.MachineURL ?? status.Name,
                    Checked = status.Checked,
                    Failed = status.Archives.Count(a => a.IsFailing),
                    Passed = status.Archives.Count(a => !a.IsFailing),
                },
                DetailedStatus = status,
                Metadata = list
            };
            Utils.Log(
                $"Writing Update for {dto.Summary.Name} - {dto.Summary.Failed} failed - {dto.Summary.Passed} passed");
            await sql.UpdateModListStatus(dto);
            Utils.Log(
                $"Done updating {dto.Summary.Name}");

        }

         private async Task<bool> IsValid(SqlService sql, ValidateModlist whitelists, Archive archive)
         {
             try
             {
                 if (!archive.State.IsWhitelisted(whitelists.ServerWhitelist)) return false;

                 try
                 {
                     if (archive.State is NexusDownloader.State state)
                     {
                         if (await ValidateNexusFast(sql, state)) return true;

                     }
                     else if (archive.State is GoogleDriveDownloader.State)
                     {
                         // Disabled for now
                         return true;
                     }
                     else if (archive.State is HTTPDownloader.State hstate &&
                              hstate.Url.StartsWith("https://wabbajack"))
                     {
                         return true;
                     }
                     else
                     {
                         if (await archive.State.Verify(archive)) return true;
                     }
                 }
                 catch (Exception)
                 {
                     // ignored
                 }

                 Utils.Log($"{archive.State.PrimaryKeyString} is broken, looking for upgrade: {archive.Name}");
                 var result = await ClientAPI.GetModUpgrade(archive.Hash);

                 if (result != null)
                 {
                     Utils.Log($"{archive.State.PrimaryKeyString} is broken, upgraded to {result.State.PrimaryKeyString} {result.Name}");
                     return true;
                 }

                 Utils.Log($"{archive.State.PrimaryKeyString} is broken, no alternative found");
                 return false;

             }
             catch (Exception ex)
             {
                 Utils.Log(ex.ToString());
                 return false;
             }
         }

         private async Task<bool> ValidateNexusFast(SqlService sql, NexusDownloader.State state)
         {
             try
             {
                 var modFiles =  await sql.GetModFiles(state.Game, state.ModID);

                 if (modFiles == null)
                 {
                     Utils.Log($"No Cache for {state.PrimaryKeyString} falling back to HTTP");
                     var nexusApi = await NexusApiClient.Get();
                     modFiles = await nexusApi.GetModFiles(state.Game, state.ModID);
                 }

                 var found = modFiles.files
                     .FirstOrDefault(file => file.file_id == state.FileID && file.category_name != null);
                 return found != null;
             }
             catch (Exception)
             {
                 return false;
             }
         }
    }
}
