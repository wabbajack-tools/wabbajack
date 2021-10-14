using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Wabbajack.Common;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.App.Models;

public class SettingsManager
{
    private readonly Configuration _configuration;
    private readonly DTOSerializer _dtos;
    private readonly ILogger<SettingsManager> _logger;

    public SettingsManager(ILogger<SettingsManager> logger, Configuration configuration, DTOSerializer dtos)
    {
        _logger = logger;
        _dtos = dtos;
        _configuration = configuration;
        _configuration.SavedSettingsLocation.CreateDirectory();
    }

    private AbsolutePath GetPath(string key) => _configuration.SavedSettingsLocation.Combine(key).WithExtension(Ext.Json);

    public async Task Save<T>(string key, T value)
    {
        var tmp = GetPath(key).WithExtension(Ext.Temp);
        await using (var s = tmp.Open(FileMode.Create, FileAccess.Write))
        {
            await JsonSerializer.SerializeAsync(s, value, _dtos.Options);
        }
        await tmp.MoveToAsync(GetPath(key), true, CancellationToken.None);
    }

    public async Task<T> Load<T>(string key)
    where T : new()
    {
        var path = GetPath(key);
        try
        {
            if (path.FileExists())
            {
                await using (var s = path.Open(FileMode.Create, FileAccess.Write))
                {
                    await JsonSerializer.DeserializeAsync<T>(s, _dtos.Options);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Loading settings {Key}", key);
        }

        return new T();
    }
}