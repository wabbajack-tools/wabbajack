using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.Common;
using Wabbajack.Server.DataLayer;

namespace Wabbajack.Server.Services;

public class MetricsKeyCache : IStartable
{
    private HashSet<string> _knownKeys = new();
    private readonly AsyncLock _lock = new();
    private ILogger<MetricsKeyCache> _logger;
    private readonly SqlService _sql;

    public MetricsKeyCache(ILogger<MetricsKeyCache> logger, SqlService sql)
    {
        _logger = logger;
        _sql = sql;
    }

    public async Task Start()
    {
        _knownKeys = _sql.AllKeys().Result.ToHashSet();
    }

    public async Task<bool> IsValidKey(string key)
    {
        using (var _ = await _lock.WaitAsync())
        {
            if (_knownKeys.Contains(key)) return true;
        }

        if (await _sql.ValidMetricsKey(key))
        {
            using var _ = await _lock.WaitAsync();
            _knownKeys.Add(key);
            return true;
        }

        return false;
    }

    public async Task AddKey(string key)
    {
        using (var _ = await _lock.WaitAsync())
        {
            if (_knownKeys.Contains(key)) return;
            _knownKeys.Add(key);
        }

        await _sql.AddMetricsKey(key);
    }

    public async Task<long> KeyCount()
    {
        using var _ = await _lock.WaitAsync();
        return _knownKeys.Count;
    }
}