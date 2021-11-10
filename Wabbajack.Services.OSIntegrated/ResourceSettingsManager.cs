using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.RateLimiter;
using Wabbajack.Services.OSIntegrated;

namespace Wabbajack.App.Models;

public class ResourceSettingsManager
{
    private readonly SettingsManager _manager;
    private Dictionary<string,ResourceSetting>? _settings;

    public ResourceSettingsManager(SettingsManager manager)
    {
        _manager = manager;
    }

    public async Task<ResourceSetting> GetSettings(string name)
    {
        Monitor.Enter(_manager);
        try
        {
            _settings ??= await _manager.Load<Dictionary<string, ResourceSetting>>("resource-settings");

            if (_settings.TryGetValue(name, out var found)) return found;

            var newSetting = new ResourceSetting
            {
                MaxTasks = Environment.ProcessorCount,
                MaxThroughput = long.MaxValue
            };
            
            _settings.Add(name, newSetting);

            await _manager.Save("resource-settings", _settings);
            
            return _settings[name];
        }
        finally
        {
            Monitor.Exit(_manager);
        }

    }

    public class ResourceSetting
    {
        public long MaxTasks { get; set; }
        public long MaxThroughput { get; set; }
    }

}