using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using Wabbajack.CLI.Builder;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Paths;
using Wabbajack.Reporting;

namespace Wabbajack.CLI.Verbs;

public class ModlistReport
{
    private readonly ILogger<ModlistReport> _logger;
    private readonly DTOSerializer _dtos;

    public ModlistReport(ILogger<ModlistReport> logger, DTOSerializer dtos)
    {
        _logger = logger;
        _dtos = dtos;
    }

    public static VerbDefinition Definition = new("modlist-report",
        "Generates a usage report for a Modlist file", new[]
        {
            new OptionDefinition(typeof(AbsolutePath), "i", "input", "Wabbajack file from which to generate a report"),
            new OptionDefinition(typeof(bool), "b", "browser", "Open report in browser after generating it (default true)")
        });

    public async Task<int> Run(AbsolutePath input, bool browser = true)
    {
        await ModlistReportGenerator.GenerateAsync(_dtos, input, _logger, browser);
        return 0;
    }
}
