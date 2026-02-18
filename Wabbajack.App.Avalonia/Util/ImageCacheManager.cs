using System;
using System.Collections.Concurrent;
using System.IO;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using static System.Text.Encoding;

namespace Wabbajack.App.Avalonia.Util;

/// <summary>
/// Disk + memory cache for modlist banner images. Stores raw PNG bytes instead of
/// WPF BitmapImage so the same cache works with Avalonia's Bitmap type.
/// </summary>
public class ImageCacheManager
{
    private readonly TimeSpan _pollInterval = TimeSpan.FromMinutes(1);
    private readonly Services.OSIntegrated.Configuration _configuration;
    private readonly ILogger<ImageCacheManager> _logger;

    private AbsolutePath _imageCachePath;
    private readonly ConcurrentDictionary<Hash, CachedImageBytes> _cachedImages = new();

    public ImageCacheManager(ILogger<ImageCacheManager> logger, Services.OSIntegrated.Configuration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        _imageCachePath = _configuration.ImageCacheLocation;
        _imageCachePath.CreateDirectory();

        Observable.Interval(_pollInterval, RxApp.TaskpoolScheduler)
            .Subscribe(__ =>
            {
                foreach (var (hash, cached) in _cachedImages)
                {
                    if (!cached.IsExpired()) continue;
                    try
                    {
                        _cachedImages.TryRemove(hash, out _);
                        var path = PathFor(hash);
                        if (path.FileExists())
                        {
                            try { File.Delete(path.ToString()); }
                            catch (IOException) { }
                            catch (UnauthorizedAccessException) { }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to delete cached image {HashHex}", hash.ToHex());
                    }
                }
            });
    }

    private AbsolutePath PathFor(Hash hash) => _imageCachePath.Combine(hash.ToHex());

    /// <summary>Stores raw PNG bytes in the memory + disk cache.</summary>
    public async Task<bool> Add(string url, byte[] pngBytes)
    {
        var hash = await UTF8.GetBytes(url).Hash();
        if (!_cachedImages.TryAdd(hash, new CachedImageBytes(pngBytes))) return false;

        var ms = new MemoryStream(pngBytes);
        var path = PathFor(hash);
        await using var fs = new FileStream(path.ToString(), FileMode.Create, FileAccess.Write, FileShare.Read);
        await ms.CopyToAsync(fs);
        return true;
    }

    /// <summary>
    /// Returns cached PNG bytes for <paramref name="url"/>, or <c>(false, null)</c> on miss.
    /// </summary>
    public async Task<(bool found, byte[]? bytes)> Get(string url)
    {
        var hash = await UTF8.GetBytes(url).Hash();

        if (_cachedImages.TryGetValue(hash, out var cached))
            return (true, cached.Bytes);

        var path = PathFor(hash);
        if (!path.FileExists()) return (false, null);

        var ms = new MemoryStream();
        await using var fs = new FileStream(path.ToString(), FileMode.Open, FileAccess.Read, FileShare.Read);
        await fs.CopyToAsync(ms);
        var bytes = ms.ToArray();
        _cachedImages.TryAdd(hash, new CachedImageBytes(bytes));
        return (true, bytes);
    }
}

public class CachedImageBytes(byte[] bytes)
{
    private readonly DateTime _cachedAt = DateTime.Now;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);

    public byte[] Bytes { get; } = bytes;

    public bool IsExpired() => DateTime.Now - _cachedAt > _cacheDuration;
}
