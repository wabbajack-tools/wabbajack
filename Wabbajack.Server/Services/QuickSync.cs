using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.Common;

namespace Wabbajack.Server.Services;

public class QuickSync
{
    private readonly AsyncLock _lock = new();
    private readonly ILogger<QuickSync> _logger;
    private readonly Dictionary<Type, IReportingService> _services = new();
    private readonly Dictionary<Type, CancellationTokenSource> _syncs = new();

    public QuickSync(ILogger<QuickSync> logger)
    {
        _logger = logger;
    }

    public async Task<Dictionary<Type, (TimeSpan Delay, TimeSpan LastRunTime, (string, DateTime)[] ActiveWork)>>
        Report()
    {
        using var _ = await _lock.WaitAsync();
        return _services.ToDictionary(s => s.Key,
            s => (s.Value.Delay, DateTime.UtcNow - s.Value.LastEnd, s.Value.ActiveWorkStatus));
    }

    public async Task Register<T>(T service)
        where T : IReportingService
    {
        using var _ = await _lock.WaitAsync();
        _services[service.GetType()] = service;
    }

    public async Task<CancellationToken> GetToken<T>()
    {
        using var _ = await _lock.WaitAsync();
        if (_syncs.TryGetValue(typeof(T), out var result)) return result.Token;
        var token = new CancellationTokenSource();
        _syncs[typeof(T)] = token;
        return token.Token;
    }

    public async Task ResetToken<T>()
    {
        using var _ = await _lock.WaitAsync();
        if (_syncs.TryGetValue(typeof(T), out var ct)) ct.Cancel();
        _syncs[typeof(T)] = new CancellationTokenSource();
    }

    public async Task Notify<T>()
    {
        _logger.LogInformation($"Quicksync {typeof(T).Name}");
        // Needs debugging
        using var _ = await _lock.WaitAsync();
        if (_syncs.TryGetValue(typeof(T), out var ct)) ct.Cancel();
    }

    public async Task Notify(Type t)
    {
        _logger.LogInformation($"Quicksync {t.Name}");
        // Needs debugging
        using var _ = await _lock.WaitAsync();
        if (_syncs.TryGetValue(t, out var ct)) ct.Cancel();
    }
}