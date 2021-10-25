using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.BuildServer;
using Wabbajack.Downloaders;
using Wabbajack.Server.DataLayer;

namespace Wabbajack.Server.Services;

public class NonNexusDownloadValidator : AbstractService<NonNexusDownloadValidator, int>
{
    private readonly DownloadDispatcher _dispatcher;
    private readonly ParallelOptions _parallelOptions;
    private readonly SqlService _sql;

    public NonNexusDownloadValidator(ILogger<NonNexusDownloadValidator> logger, AppSettings settings, SqlService sql,
        QuickSync quickSync, DownloadDispatcher dispatcher, ParallelOptions parallelOptions)
        : base(logger, settings, quickSync, TimeSpan.FromHours(2))
    {
        _sql = sql;
        _dispatcher = dispatcher;
        _parallelOptions = parallelOptions;
    }

    public override async Task<int> Execute()
    {
        var archives = await _sql.GetNonNexusModlistArchives();
        _logger.Log(LogLevel.Information, $"Validating {archives.Count} non-Nexus archives");
        await _dispatcher.PrepareAll(archives.Select(a => a.State));
        var random = new Random();

        /*
        var results = await archives.PMap(_parallelOptions, async archive =>
        {
            try
            {
                await Task.Delay(random.Next(1000, 5000));
                
                var token = new CancellationTokenSource();
                token.CancelAfter(TimeSpan.FromMinutes(10));
                
                ReportStarting(archive.State.PrimaryKeyString);
                bool isValid = false;
                switch (archive.State)
                {
                    //case WabbajackCDNDownloader.State _: 
                    //case GoogleDriveDownloader.State _: // Let's try validating Google again 2/10/2021
                    case GameFileSource _:
                        isValid = true;
                        break;
                    case Manual _:
                    case ModDB _:
                    case Http h when h.Url.ToString().StartsWith("https://wabbajack"):
                        isValid = true;
                        break;
                    default:
                        isValid = await _dispatcher.Verify(archive, token.Token);
                        break;
                }
                return (Archive: archive, IsValid: isValid);
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Warning, $"Error for {archive.Name} {archive.State.PrimaryKeyString} {ex}");
                return (Archive: archive, IsValid: false);
            }
            finally
            {
                ReportEnding(archive.State.PrimaryKeyString);
            }

        }).ToList();

        await _sql.UpdateNonNexusModlistArchivesStatus(results);
        var failed = results.Count(r => !r.IsValid);
        var passed = results.Count() - failed;
        foreach(var (archive, _) in results.Where(f => !f.IsValid))
            _logger.Log(LogLevel.Warning, $"Validation failed for {archive.Name} from {archive.State.PrimaryKeyString}");
        
        _logger.Log(LogLevel.Information, $"Non-nexus validation completed {failed} out of {passed} failed");
        */
        return default;
    }
}