using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.Common;

namespace Wabbajack.Server.Services
{
    public class QuickSync
    {
        private Dictionary<Type, CancellationTokenSource> _syncs = new Dictionary<Type, CancellationTokenSource>();
        private Dictionary<Type, IReportingService> _services = new Dictionary<Type, IReportingService>();
        private AsyncLock _lock = new AsyncLock();
        private ILogger<QuickSync> _logger;

        public QuickSync(ILogger<QuickSync> logger)
        {
            _logger = logger;
        }

        public async Task<Dictionary<Type, (TimeSpan Delay, TimeSpan LastRunTime)>> Report()
        {
            using var _ = await _lock.WaitAsync();
            return _services.ToDictionary(s => s.Key,
                s => (s.Value.Delay, DateTime.UtcNow - s.Value.LastEnd));
        }

        public async Task Register<T>(T service)
        where T : IReportingService
        {
            using var _ = await _lock.WaitAsync();
            _services[typeof(T)] = service;
        }

        public async Task<CancellationToken> GetToken<T>()
        {
            using var _ = await _lock.WaitAsync();
            if (_syncs.TryGetValue(typeof(T), out var result))
            {
                return result.Token;
            }
            var token = new CancellationTokenSource();
            _syncs[typeof(T)] = token;
            return token.Token;
        }

        public async Task ResetToken<T>()
        {
            using var _ = await _lock.WaitAsync();
            if (_syncs.TryGetValue(typeof(T), out var ct))
            {
                ct.Cancel();
            }
            _syncs[typeof(T)] = new CancellationTokenSource();
        }

        public async Task Notify<T>()
        {
            _logger.LogInformation($"Quicksync {typeof(T).Name}");
            // Needs debugging
            using var _ = await _lock.WaitAsync();
            if (_syncs.TryGetValue(typeof(T), out var ct))
            {
                ct.Cancel();
            }
        }
    }
}
