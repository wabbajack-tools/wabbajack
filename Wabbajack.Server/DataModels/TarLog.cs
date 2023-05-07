using Microsoft.Extensions.Logging;
using Wabbajack.BuildServer;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Server.DataModels;

public class TarLog
{
    private Task<HashSet<string>> _tarKeys;
    private readonly AppSettings _settings;
    private readonly ILogger<TarLog> _logger;

    public TarLog(AppSettings settings, ILogger<TarLog> logger)
    {
        _settings = settings;
        _logger = logger;
        Load();
    }

    private void Load()
    {
        if (_settings.TarKeyFile.ToAbsolutePath().FileExists())
        {
            _tarKeys = Task.Run(async () =>
            {
                var keys = await _settings.TarKeyFile.ToAbsolutePath()
                    .ReadAllLinesAsync()
                    .Select(line => line.Trim())
                    .ToHashSetAsync();
                _logger.LogInformation("Loaded {Count} tar keys", keys.Count);
                return keys;
            });
        }
        else
        {
            _tarKeys = Task.Run(async () => new HashSet<string>());
        }

    }

    public async Task<bool> Contains(string metricsKey)
    {
        return (await _tarKeys).Contains(metricsKey);
    }
    
    
}