using System;
using System.Threading.Tasks;
using Wabbajack.BuildServer.Model.Models;
using Wabbajack.Common;

namespace Wabbajack.BuildServer.BackendServices
{
    public class ValidateNonNexusArchives : ABackendService
    {
        public ValidateNonNexusArchives(SqlService sql, AppSettings settings) : base(sql, settings, TimeSpan.FromHours(2))
        {
        }

        public override async Task Execute()
        {
            var archives = await Sql.GetNonNexusModlistArchives();
            using var queue = new WorkQueue();
            var results = await archives.PMap(queue, async archive =>
            {
                var isValid = await archive.State.Verify(archive);
                return (Archive: archive, IsValid: isValid);
            });

            await Sql.UpdateNonNexusModlistArchivesStatus(results);
        }
    }
}
