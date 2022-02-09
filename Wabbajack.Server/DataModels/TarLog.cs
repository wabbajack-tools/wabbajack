using Wabbajack.BuildServer;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Server.DataModels;

public class TarLog
{
    private Task<HashSet<string>> _tarKeys;
    private readonly AppSettings _settings;

    public TarLog(AppSettings settings)
    {
        _settings = settings;
        Load();
    }

    private void Load()
    {
        if (_settings.TarKeyFile.ToAbsolutePath().FileExists())
        {
            _tarKeys = Task.Run(async () => await _settings.TarKeyFile.ToAbsolutePath()
                .ReadAllLinesAsync()
                .Select(line => line.Trim())
                .ToHashSetAsync());
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