using System;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Logging;
using Splat;
using Wabbajack.BuildServer;
using Wabbajack.Common;
using Wabbajack.Lib.Downloaders;
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
            _logger.Log(LogLevel.Information, $"Validating {archives.Count} non-Nexus archives");
            using var queue = new WorkQueue();
            await DownloadDispatcher.PrepareAll(archives.Select(a => a.State));

            var results = await archives.PMap(queue, async archive =>
            {
                try
                {
                    bool isValid = false;
                    switch (archive.State)
                    {
                        case WabbajackCDNDownloader.State _:
                        case GoogleDriveDownloader.State _:
                        case ManualDownloader.State _:
                        case ModDBDownloader.State _:    
                        case HTTPDownloader.State h when h.Url.StartsWith("https://wabbajack"):
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
                    _logger.Log(LogLevel.Warning, $"Error for {archive.Name} {archive.State.PrimaryKeyString} {ex}");
                    return (Archive: archive, IsValid: false);
                }

            });

            await _sql.UpdateNonNexusModlistArchivesStatus(results);
            var failed = results.Count(r => !r.IsValid);
            var passed = results.Count() - failed;
            foreach(var (archive, _) in results.Where(f => !f.IsValid))
                _logger.Log(LogLevel.Warning, $"Validation failed for {archive.Name} from {archive.State.PrimaryKeyString}");
            
            _logger.Log(LogLevel.Information, $"Non-nexus validation completed {failed} out of {passed} failed");
            return failed;
        }
    }
}
