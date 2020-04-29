using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Wabbajack.BuildServer.Controllers;
using Wabbajack.BuildServer.Model.Models;
using Wabbajack.Common;
using Wabbajack.Lib.Downloaders;

namespace Wabbajack.BuildServer.BackendServices
{
    public class ValidateNonNexusArchives : ABackendService
    {
        public ValidateNonNexusArchives(SqlService sql, AppSettings settings) : base(sql, settings, TimeSpan.FromHours(2))
        {
        }

        public override async Task Execute()
        {
            Utils.Log("Updating Non Nexus archives");
            var archives = await Sql.GetNonNexusModlistArchives();
            Utils.Log($"Validating {archives.Count} Non-Nexus archives.");
            using var queue = new WorkQueue();
            await DownloadDispatcher.PrepareAll(archives.Select(a => a.State));
            var results = await archives.PMap(queue, async archive =>
            {
                try
                {
                    bool isValid;
                    switch (archive.State)
                    {
                        case GoogleDriveDownloader.State _:
                        case ManualDownloader.State _:
                        case HTTPDownloader.State s when new Uri(s.Url).Host.StartsWith("wabbajackpush"):
                            isValid = true;
                            break;
                        default:
                            isValid = await archive.State.Verify(archive);
                            break;
                    }
                    return (Archive: archive, IsValid: isValid);
                }
                catch (Exception ex)
                {
                    Utils.Log($"Got Validation error {ex}");
                    return (Archive: archive, IsValid: false);
                }

            });

            await Sql.UpdateNonNexusModlistArchivesStatus(results);
        }
    }
}
