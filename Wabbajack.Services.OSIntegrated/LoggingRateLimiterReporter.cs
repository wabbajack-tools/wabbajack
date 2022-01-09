using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;
using Wabbajack.Common;
using Wabbajack.RateLimiter;

namespace Wabbajack.Services.OSIntegrated;

public class LoggingRateLimiterReporter : IDisposable
{
    private readonly IEnumerable<IResource> _limiters;
    private readonly ILogger<LoggingRateLimiterReporter> _logger;
    private readonly Timer _timer;
    private StatusReport[] _prevReport;
    private long _reportNumber;

    public LoggingRateLimiterReporter(ILogger<LoggingRateLimiterReporter> logger, IEnumerable<IResource> limiters)
    {
        _logger = logger;
        _limiters = limiters.ToArray();
        _timer = new Timer(StartLoop, null, TimeSpan.FromSeconds(0.25), TimeSpan.FromSeconds(1));
        _prevReport = NextReport();
    }

    public void Dispose()
    {
        _timer.Dispose();
    }

    private StatusReport[] NextReport()
    {
        return _limiters.Select(r => r.StatusReport).ToArray();
    }

    private void StartLoop(object? state)
    {
        _reportNumber += 1;
        var report = NextReport();
        var sb = new StringBuilder();
        sb.Append($"[#{_reportNumber}] ");
        
        var found = false;
        foreach (var (prev, next, limiter) in _prevReport.Zip(report, _limiters))
        {
            var throughput = next.Transferred - prev.Transferred;
            if (throughput > 0)
            {
                found = true;
                sb.Append($"{limiter.Name}: [{next.Running}/{next.Pending}] {throughput.ToFileSizeString()}/sec ");
            }
        }

        if (found) 
            _logger.LogInformation(sb.ToString());
        
        _prevReport = report;
    }
}