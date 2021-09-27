using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;
using Wabbajack.Common;
using Wabbajack.RateLimiter;

namespace Wabbajack.Services.OSIntegrated
{
    public class LoggingRateLimiterReporter : IDisposable
    {
        private readonly Timer _timer;
        private long _reportNumber = 0;
        private readonly ILogger<LoggingRateLimiterReporter> _logger;
        private readonly IEnumerable<IResource> _limiters;
        private StatusReport[] _prevReport;

        public LoggingRateLimiterReporter(ILogger<LoggingRateLimiterReporter> logger, IEnumerable<IResource> limiters)
        {
            _logger = logger;
            _limiters = limiters.ToArray();
            _timer = new Timer(StartLoop, null, TimeSpan.FromSeconds(0.25), TimeSpan.FromSeconds(1));
            _prevReport = NextReport();
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
            foreach (var (prev, next, limiter) in _prevReport.Zip(report, _limiters))
            {
                var throughput = next.Transferred - prev.Transferred;
                sb.Append($"{limiter.Name}: [{next.Running}/{next.Pending}] {throughput.ToFileSizeString()}/sec ");
            }
            _logger.LogInformation(sb.ToString());
            _prevReport = report;
        }

        public void Dispose()
        {
            _timer.Dispose();
        }
    }
}