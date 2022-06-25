using System.Reflection;
using System.Text.Json;
using Chronic.Core;
using CouchDB.Driver;
using CouchDB.Driver.Views;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Nettle;
using Wabbajack.Common;
using Wabbajack.DTOs.ServerResponses;
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
    private readonly ICouchDatabase<Metric> _db;

    public MetricsController(ILogger<MetricsController> logger, Metrics metricsStore,
        AppSettings settings, ICouchDatabase<Metric> db)
    {
        _logger = logger;
        _settings = settings;
        _metricsStore = metricsStore;
        _db = db;
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

        await _db.AddAsync(new Metric
        {
            Timestamp = date,
            Action = subject,
            Subject = value,
            MetricsKey = metricsKey,
            UserAgent = Request.Headers.UserAgent.FirstOrDefault() ?? "<unknown>",
            Ip = Request.Headers["cf-connecting-ip"].FirstOrDefault() ??
                 (Request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "")
        });

        return new Result {Timestamp = date};
    }

    private static byte[] EOL = {(byte)'\n'};
    private static byte[] LBRACKET = {(byte)'['};
    private static byte[] RBRACKET = {(byte)']'};
    private static byte[] COMMA = {(byte) ','};

    [HttpGet]
    [Route("dump")]
    public async Task GetMetrics([FromQuery] string action, [FromQuery] string from, [FromQuery] string? to, [FromQuery] string? subject)
    {
        throw new NotImplementedException();
        var parser = new Parser();
        
        to ??= "now";

        var toDate = parser.Parse(to).Start;
        var fromDate = parser.Parse(from).Start;

        var records = _metricsStore.GetRecords(fromDate!.Value, toDate!.Value, action);

        Response.Headers.ContentType = "application/json";
        await foreach (var record in records)
        {
            if (!string.IsNullOrWhiteSpace(subject) && !record.Subject.Contains(subject))
                continue;
            
            await JsonSerializer.SerializeAsync(Response.Body, record);
            await Response.Body.WriteAsync(EOL);
        }
    }

    [HttpGet]
    [Route("report")]
    [ResponseCache(Duration = 60 * 60 * 4, VaryByQueryKeys = new [] {"action", "from", "to"})]
    public async Task GetReport([FromQuery] string action, [FromQuery] string from, [FromQuery] string? to)
    {
        var parser = new Parser();
        
        to ??= "now";

        var toDate = parser.Parse(to).Start!.Value.TruncateToDate();
        
        var groupFilterStart = parser.Parse("three days ago").Start!.Value.TruncateToDate();
        toDate = new DateTime(toDate.Year, toDate.Month, toDate.Day);

        var prefetch = (await GetByAction(action, groupFilterStart, toDate))
            .Select(d => d.Subject)
            .ToHashSet();

        var fromDate = parser.Parse(from).Start!.Value.TruncateToDate();

        var counts = (await GetByAction(action, fromDate, toDate))
            .Where(r => prefetch.Contains(r.Subject))
            .ToDictionary(kv => (kv.Date, kv.Subject), kv => kv.Count);
        
        Response.Headers.ContentType = "application/json";
        var row = new Dictionary<string, object>();

        Response.Body.Write(LBRACKET);
        for (var d = fromDate; d <= toDate; d = d.AddDays(1))
        {
            row["_Timestamp"] = d;
            foreach (var group in prefetch)
            {
                if (counts.TryGetValue((d, group), out var found))
                    row[group] = found;
                else
                    row[group] = 0;
            }
            await JsonSerializer.SerializeAsync(Response.Body, row);
            Response.Body.Write(EOL);
            if (d != toDate)
                Response.Body.Write(COMMA);
        }

        Response.Body.Write(RBRACKET);

    }
    
    
    private async Task<IReadOnlyList<(DateTime Date, string Subject, long Count)>> GetByAction(string action, DateTime from, DateTime to)
    {
        var records = await _db.GetViewAsync<object?[], long>("Indexes", "ActionDaySubject",
            new CouchViewOptions<object?[]>
            {
                StartKey = new object?[]{action, from.Year, from.Month, from.Day, null},
                EndKey = new object?[]{action, to.Year, to.Month, to.Day, new()},
                Reduce = true,
                GroupLevel = 10,
                Group = true
            });
        
        var results = records
            .Where(r => r.Key.Length >= 4 && r.Key[4] != null)
            .Select(r => 
            (new DateTime((int)(long)r.Key[1]!, (int)(long)r.Key[2]!, (int)(long)r.Key[3]!), (string)r.Key[4]!, r.Value));
        return results.ToList();
    }
    

    public class Result
    {
        public DateTime Timestamp { get; set; }
    }
}