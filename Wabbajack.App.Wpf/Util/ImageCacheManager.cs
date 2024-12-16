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
        var path = _imageCachePath.Combine(hash.ToHex());
        await using var fs = new FileStream(path.ToString(), FileMode.Create, FileAccess.Write);
        ms.WriteTo(fs);
    }
    private async Task<(bool, MemoryStream)> LoadImage(Hash hash)
    {
        MemoryStream imageStream = null;
        var path = _imageCachePath.Combine(hash.ToHex());
        if (!path.FileExists())
        {
            return (false, imageStream);
        }

        imageStream = new MemoryStream();
        await using var fs = new FileStream(path.ToString(), FileMode.Open, FileAccess.Read);
        await fs.CopyToAsync(imageStream);
        return (true, imageStream);
    }

    public ImageCacheManager(ILogger<ImageCacheManager> logger, Services.OSIntegrated.Configuration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        _imageCachePath = _configuration.ImageCacheLocation;
        _imageCachePath.CreateDirectory();
        
        RxApp.TaskpoolScheduler.ScheduleRecurringAction(_pollInterval, () =>
        {
            foreach (var (hash, cachedImage) in _cachedImages)
            {
                if (!cachedImage.IsExpired()) continue;
                
                try
                {
                    _cachedImages.TryRemove(hash, out _);
                    File.Delete(_configuration.ImageCacheLocation.Combine(hash).ToString());
                }
                catch (Exception ex)
                {
                    _logger.LogError("Failed to delete cached image {b64}", hash);
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
    
    public bool IsExpired() => _cachedAt - DateTime.Now > _cacheDuration;
}