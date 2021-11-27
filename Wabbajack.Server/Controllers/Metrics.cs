using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Nettle;
using Wabbajack.Common;
using Wabbajack.Server.DataModels;
using Wabbajack.Server.DTOs;

namespace Wabbajack.BuildServer.Controllers;

[ApiController]
[Route("/metrics")]
public class MetricsController : ControllerBase
{
    private static readonly Func<object, string> ReportTemplate = NettleEngine.GetCompiler().Compile(@"
            <html><body>
                <h2>Tar Report for {{$.key}}</h2>
                <h3>Ban Status: {{$.status}}</h3>
                <table>
                {{each $.log }}
                <tr>
                <td>{{$.Timestamp}}</td>
                <td>{{$.Path}}</td>
                <td>{{$.Key}}</td>
                </tr>
                {{/each}}
                </table>
            </body></html>
        ");

    private static Func<object, string> _totalListTemplate;
    private readonly AppSettings _settings;
    private ILogger<MetricsController> _logger;
    private readonly Metrics _metricsStore;

    public MetricsController(ILogger<MetricsController> logger, Metrics metricsStore,
        AppSettings settings)
    {
        _logger = logger;
        _settings = settings;
        _metricsStore = metricsStore;
    }


    private static Func<object, string> TotalListTemplate
    {
        get
        {
            if (_totalListTemplate == null)
            {
                var resource = Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream("Wabbajack.Server.Controllers.Templates.TotalListTemplate.html")!
                    .ReadAllText();
                _totalListTemplate = NettleEngine.GetCompiler().Compile(resource);
            }

            return _totalListTemplate;
        }
    }

    [HttpGet]
    [Route("{subject}/{value}")]
    public async Task<Result> LogMetricAsync(string subject, string value)
    {
        var date = DateTime.UtcNow;
        var metricsKey = Request.Headers[_settings.MetricsKeyHeader].FirstOrDefault();

        // Used in tests
        if (value is "Default" or "untitled" || subject == "failed_download" || Guid.TryParse(value, out _))
            return new Result {Timestamp = date};

        await _metricsStore.Ingest(new Metric
        {
            Timestamp = DateTime.UtcNow, 
            Action = subject, 
            Subject = subject, 
            MetricsKey = metricsKey,
            UserAgent = Request.Headers.UserAgent.FirstOrDefault() ?? "<unknown>",
        });
        return new Result {Timestamp = date};
    }

    public class Result
    {
        public DateTime Timestamp { get; set; }
    }
}