using System;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Logging;
using Splat;
using Wabbajack.BuildServer;
using Wabbajack.Common;
using Wabbajack.Server.DataLayer;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Wabbajack.Server.Services
{
    public class NonNexusDownloadValidator : AbstractService<NonNexusDownloadValidator, int>
    {
        private SqlService _sql;

        public NonNexusDownloadValidator(ILogger<NonNexusDownloadValidator> logger, AppSettings settings, SqlService sql)
            : base(logger, settings, TimeSpan.FromHours(2))
        {
            _sql = sql;
        }

        public override async Task<int> Execute()
        {
            var archives = await _sql.GetNonNexusModlistArchives();
            _logger.Log(LogLevel.Information, "Validating {archives.Count} non-Nexus archives");
            using var queue = new WorkQueue();
            var results = await archives.PMap(queue, async archive =>
            {
                try
                {
                    var isValid = await archive.State.Verify(archive);
                    return (Archive: archive, IsValid: isValid);
                }
                catch (Exception)
                {
                    return (Archive: archive, IsValid: false);
                }

            });

            await _sql.UpdateNonNexusModlistArchivesStatus(results);
            var failed = results.Count(r => !r.IsValid);
            var passed = results.Count() - failed;
            foreach(var (archive, _) in results.Where(f => f.IsValid))
                _logger.Log(LogLevel.Warning, $"Validation failed for {archive.Name} from {archive.State.PrimaryKeyString}");
            
            _logger.Log(LogLevel.Information, $"Non-nexus validation completed {failed} out of {passed} failed");
            return failed;
        }
    }
}
