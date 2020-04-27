using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Wabbajack.BuildServer.Model.Models;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.ModListRegistry;

namespace Wabbajack.BuildServer.BackendServices
{
    public class ListIngest : ABackendService
    {
        public ListIngest(SqlService sql, AppSettings settings) : base(sql, settings, TimeSpan.FromMinutes(1))
        {
        }

        public override async Task Execute()
        {
            var client = new Common.Http.Client();
            var lists = await client.GetJsonAsync<List<ModlistMetadata>>(Consts.ModlistMetadataURL);
            bool newData = false;
            foreach (var list in lists)
            {
                if (await Sql.HaveIndexedModlist(list.Links.MachineURL, list.DownloadMetadata.Hash))
                    continue;
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

                ModList modlist;
                await using (var fs = modlistPath.OpenRead())
                using (var zip = new ZipArchive(fs, ZipArchiveMode.Read))
                await using (var entry = zip.GetEntry("modlist")?.Open())
                {
                    if (entry == null)
                    {
                        Utils.Log($"Bad Modlist {list.Links.MachineURL}");
                        continue;
                    }

                    try
                    {
                        modlist = entry.FromJson<ModList>();
                    }
                    catch (JsonReaderException ex)
                    {
                        Utils.Log($"Bad JSON format for {list.Links.MachineURL}");
                        continue;
                    }
                }

                newData = true;
                await Sql.IngestModList(list.DownloadMetadata.Hash, list, modlist);
            }

            if (newData)
            {
                var service = new ValidateNonNexusArchives(Sql, Settings);
                await service.Execute();
            }
          
        }
    }
}
