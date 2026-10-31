using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.Common;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Services.OSIntegrated;

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

    private AbsolutePath GetPath(string key)
    {
        return _configuration.SavedSettingsLocation.Combine(key).WithExtension(Ext.Json);
    }

    public async Task Save<T>(string key, T value)
    {
        // A temporary file of its own for each save: with one per key, a save running alongside another (the app
        // and the CLI, or two processes on one machine) could rename the other's away, and that one's rename then
        // found nothing. The last rename wins, which for settings is what is wanted.
        var path = GetPath(key);
        var tmp = path.Parent.Combine($"{path.FileName}.{RandomName.Next()}").WithExtension(Ext.Temp);
        await using (var s = tmp.Open(FileMode.Create, FileAccess.Write))
        {
            var opts = new JsonSerializerOptions(_dtos.Options)
            {
                WriteIndented = true
            };
            await JsonSerializer.SerializeAsync(s, value, opts);
        }

        await tmp.MoveToAsync(path, true, CancellationToken.None);
    }

    public async Task<T> Load<T>(string key)
        where T : new()
    {
        var path = GetPath(key);
        try
        {
            if (path.FileExists())
            {
                await using (var s = path.Open(FileMode.Open))
                {
                    return (await JsonSerializer.DeserializeAsync<T>(s, _dtos.Options))!;
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