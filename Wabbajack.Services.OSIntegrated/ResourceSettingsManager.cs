using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Wabbajack.Services.OSIntegrated;

public class ResourceSettingsManager
{
    private readonly SettingsManager _manager;
    private Dictionary<string,ResourceSetting>? _settings = null;

    public ResourceSettingsManager(SettingsManager manager)
    {
        _manager = manager;
    }

    private SemaphoreSlim _lock = new(1);

    public async Task<ResourceSetting> GetSetting(string name)
    {
        await _lock.WaitAsync();
        try
        {
            _settings ??= await _manager.Load<Dictionary<string, ResourceSetting>>("resource_settings");
            if (!_settings.ContainsKey(name))
            {
                var newSetting = new ResourceSetting
                {
                    MaxTasks = Environment.ProcessorCount,
                    MaxThroughput = 0
                };

                _settings.Add(name, newSetting);
                await SaveSettings(_settings);
            }

            var setting = _settings[name];
            return setting;
        }
        finally
        {
            _lock.Release();
        }
    }
    public async Task SetSetting(string name, ResourceSetting setting)
    {
        await _lock.WaitAsync();
        try
        {
            _settings ??= await _manager.Load<Dictionary<string, ResourceSetting>>("resource_settings");
            _settings[name] = setting;
            await SaveSettings(_settings);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Dictionary<string, ResourceSetting>> GetSettings()
    {
        _settings ??= await _manager.Load<Dictionary<string, ResourceSetting>>("resource_settings");
        return _settings;
    }

    public class ResourceSetting
    {
        public long MaxTasks { get; set; }
        public long MaxThroughput { get; set; }
    }

    public async Task SaveSettings(Dictionary<string, ResourceSetting> settings)
    {
        await _manager.Save("resource_settings", settings);
    }
}