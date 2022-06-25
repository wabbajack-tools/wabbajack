using System.Text.Json;
using K4os.Compression.LZ4.Internal;
using Microsoft.Extensions.Logging;
using Wabbajack.BuildServer;
using Wabbajack.Common;
using Wabbajack.DTOs;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Networking.NexusApi;
using Wabbajack.Networking.NexusApi.DTOs;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.Server.DTOs;

namespace Wabbajack.Server.Services;

public class NexusCacheManager
{
    private readonly ILogger<NexusCacheManager> _loggger;
    private readonly DTOSerializer _dtos;
    private readonly AppSettings _configuration;
    private readonly AbsolutePath _cacheFolder;
    private readonly SemaphoreSlim _lockObject;
    private readonly NexusApi _nexusAPI;
    private readonly Timer _timer;
    private readonly DiscordWebHook _discord;

    public NexusCacheManager(ILogger<NexusCacheManager> logger, DTOSerializer dtos, AppSettings configuration, NexusApi nexusApi, DiscordWebHook discord)
    {
        _loggger = logger;
        _dtos = dtos;
        _configuration = configuration;
        _cacheFolder = configuration.NexusCacheFolder.ToAbsolutePath();
        _lockObject = new SemaphoreSlim(1);
        _nexusAPI = nexusApi;
        _discord = discord;

        if (configuration.RunBackendNexusRoutines)
        {
            _timer = new Timer(_ => UpdateNexusCacheAPI().FireAndForget(), null, TimeSpan.FromSeconds(2),
                TimeSpan.FromHours(4));
        }
    }
    
    
    private AbsolutePath CacheFile(string key)
    {
        return _cacheFolder.Combine(key).WithExtension(Ext.Json);
    }

        
    private bool HaveCache(string key)
    {
        return CacheFile(key).FileExists();
    }

    public async Task SaveCache<T>(string key, T value, CancellationToken token)
    {
        var ms = new MemoryStream();
        await JsonSerializer.SerializeAsync(ms, value, _dtos.Options, token);
        await ms.FlushAsync(token);
        var data = ms.ToArray();
        await _lockObject.WaitAsync(token);
        try
        {
            await CacheFile(key).WriteAllBytesAsync(data, token: token);
        }
        finally
        {
            _lockObject.Release();
        }
    }

    public async Task<T?> GetCache<T>(string key, CancellationToken token)
    {
        if (!HaveCache(key)) return default;
        
        var file = CacheFile(key);
        await _lockObject.WaitAsync(token);
        byte[] data;
        try
        {
            data = await file.ReadAllBytesAsync(token);
        }
        catch (FileNotFoundException ex)
        {
            return default;
        }
        finally
        {
            _lockObject.Release();
        }
        return await JsonSerializer.DeserializeAsync<T>(new MemoryStream(data), _dtos.Options, token);
    }

    public async Task UpdateNexusCacheAPI()
    {
        var gameTasks = GameRegistry.Games.Values
            .Where(g => g.NexusName != null)
            .SelectAsync(async game =>
            {
                    var mods = await _nexusAPI.GetUpdates(game.Game, CancellationToken.None);
                    return (game, mods); 
            });


        var purgeList = new List<(string Key, DateTime Date)>();
        
        await foreach (var (game, mods) in gameTasks)
        {
            foreach (var mod in mods.Item1)
            {
                var date = Math.Max(mod.LastestModActivity, mod.LatestFileUpdate).AsUnixTime();
                purgeList.Add(($"_{game.Game.MetaData().NexusName!.ToLowerInvariant()}_{mod.ModId}_", date));
            }
        }

        // This is O(m * n) where n and m are 15,000 items, we really should improve this
        var files = (from file in _cacheFolder.EnumerateFiles().AsParallel()
            from entry in purgeList
            where file.FileName.ToString().Contains(entry.Key)
            where file.LastModifiedUtc() < entry.Date
            select file).ToHashSet();

        foreach (var file in files)
        {
            await PurgeCacheEntry(file);
        }
        
        await _discord.Send(Channel.Ham, new DiscordMessage
        {
            Content = $"Cleared {files.Count} Nexus cache entries due to updates"
        });
    }

    private async Task PurgeCacheEntry(AbsolutePath file)
    {
        await _lockObject.WaitAsync();
        try
        {
            if (file.FileExists()) file.Delete();
        }
        catch (FileNotFoundException)
        {
            return;
        }
        finally
        {
            _lockObject.Release();
        }
    }

    public async Task<int> Purge(string mod)
    {
        if (Uri.TryCreate(mod, UriKind.Absolute, out var url))
        {
            mod = Enumerable.Last(url.AbsolutePath.Split("/", StringSplitOptions.RemoveEmptyEntries));
        }

        var count = 0;
        if (!int.TryParse(mod, out var mod_id)) return count;
        
        foreach (var file in _cacheFolder.EnumerateFiles())
        {
            if (!file.FileName.ToString().Contains($"_{mod_id}")) continue;
            
            await PurgeCacheEntry(file);
            count++;
        }

        return count;
    }
}