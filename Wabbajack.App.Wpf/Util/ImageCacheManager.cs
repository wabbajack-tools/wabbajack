using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using DynamicData.Kernel;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using static System.Text.Encoding;
using Convert = System.Convert;

namespace Wabbajack;

public class ImageCacheManager
{
    private readonly TimeSpan _pollInterval = TimeSpan.FromMinutes(1);
    private readonly Services.OSIntegrated.Configuration _configuration;
    private readonly ILogger<ImageCacheManager> _logger;
    
    private AbsolutePath _imageCachePath;
    private ConcurrentDictionary<Hash, CachedImage> _cachedImages { get; } = new();

    private async Task SaveImage(Hash hash, MemoryStream ms)
    {
        var path = PathFor(hash);
        ms.Position = 0;
        await using var fs = new FileStream(path.ToString(), FileMode.Create, FileAccess.Write, FileShare.Read);
        await ms.CopyToAsync(fs);
    }

    private async Task<(bool ok, MemoryStream stream)> LoadImage(Hash hash)
    {
        var path = PathFor(hash);
        if (!path.FileExists()) return (false, null);

        var ms = new MemoryStream();
        await using var fs = new FileStream(path.ToString(), FileMode.Open, FileAccess.Read, FileShare.Read);
        await fs.CopyToAsync(ms);
        return (true, ms);
    }

    private AbsolutePath PathFor(Hash hash) => _imageCachePath.Combine(hash.ToHex());

    public ImageCacheManager(ILogger<ImageCacheManager> logger, Services.OSIntegrated.Configuration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        _imageCachePath = _configuration.ImageCacheLocation;
        _imageCachePath.CreateDirectory();

        RxApp.TaskpoolScheduler.ScheduleRecurringAction(_pollInterval, () =>
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
                        catch (IOException) {  }
                        catch (UnauthorizedAccessException) {  }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to delete cached image {hashHex}", hash.ToHex());
                }
            }
        });


    }

    public async Task<bool> Add(string url, BitmapImage img)
    {
        var hash = await UTF8.GetBytes(url).Hash();
        if (!_cachedImages.TryAdd(hash, new CachedImage(img))) return false;
        
        await SaveImage(hash, (MemoryStream)img.StreamSource);
        return true;

    }

    public async Task<(bool, BitmapImage)> Get(string url)
    {
        var hash = await UTF8.GetBytes(url).Hash();
        // Try to load the image from memory
        if (_cachedImages.TryGetValue(hash, out var cachedImage)) return (true, cachedImage.Image);

        // Try to load the image from disk
        var (success, imageStream) = await LoadImage(hash);
        if (!success) return (false, null);
        
        var img = UIUtils.BitmapImageFromStream(imageStream);
        _cachedImages.TryAdd(hash, new CachedImage(img));
        await imageStream.DisposeAsync();
        return (true, img);

    }
}

public class CachedImage(BitmapImage image)
{
    private readonly DateTime _cachedAt = DateTime.Now;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);
    
    public BitmapImage Image { get; } = image;

    public bool IsExpired() => DateTime.Now - _cachedAt > _cacheDuration;
}