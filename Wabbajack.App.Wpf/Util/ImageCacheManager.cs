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
using System.Threading;

namespace Wabbajack;

public class ImageCacheManager
{
    private readonly TimeSpan _pollInterval = TimeSpan.FromMinutes(1);
    private readonly Services.OSIntegrated.Configuration _configuration;
    private readonly ILogger<ImageCacheManager> _logger;
    private readonly ConcurrentDictionary<Hash, SemaphoreSlim> _writeLocks = new();


    private AbsolutePath _imageCachePath;
    private ConcurrentDictionary<Hash, CachedImage> _cachedImages { get; } = new();

    private SemaphoreSlim LockFor(Hash hash) =>
        _writeLocks.GetOrAdd(hash, _ => new SemaphoreSlim(1, 1));

    private async Task SaveImageAtomic(Hash hash, byte[] bytes)
    {
        var path = PathFor(hash);
        var tmp = path.ToString() + ".tmp";

        await using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await fs.WriteAsync(bytes, 0, bytes.Length);
            await fs.FlushAsync();
        }

        try
        {
            if (File.Exists(path.ToString()))
            {
                File.Replace(tmp, path.ToString(), null);
            }
            else
            {
                File.Move(tmp, path.ToString());
            }
        }
        finally
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
        }
    }

    private async Task<(bool ok, MemoryStream stream)> LoadImage(Hash hash)
    {
        var path = PathFor(hash);
        if (!path.FileExists()) return (false, null);

        var ms = new MemoryStream();
        await using var fs = new FileStream(path.ToString(), FileMode.Open, FileAccess.Read, FileShare.Read);
        await fs.CopyToAsync(ms);
        ms.Position = 0;
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
        if (img.StreamSource is null) return false;

        if (img.StreamSource.CanSeek) img.StreamSource.Position = 0;
        await using var copy = new MemoryStream();
        await img.StreamSource.CopyToAsync(copy);
        var bytes = copy.ToArray();

        return await AddBytes(url, bytes);
    }

    public async Task<(bool, BitmapImage)> Get(string url)
    {
        var hash = await UTF8.GetBytes(url + "|pngcache-v2").Hash();

        if (_cachedImages.TryGetValue(hash, out var cachedImage))
            return (true, cachedImage.Image);

        var path = PathFor(hash);
        var (success, imageStream) = await LoadImage(hash);
        if (!success) return (false, null);

        try
        {
            if (imageStream.Length < 8)
                throw new InvalidDataException("Cached image too small");

            var header = new byte[8];
            await imageStream.ReadAsync(header, 0, 8);
            imageStream.Position = 0;

            // PNG signature: 89 50 4E 47 0D 0A 1A 0A
            var isPng = header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
                        header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A;

            if (!isPng)
                throw new InvalidDataException("Cached image is not PNG (likely old WebP cache)");

            var img = UIUtils.BitmapImageFromStream(imageStream);
            _cachedImages.TryAdd(hash, new CachedImage(img));
            return (true, img);
        }
        catch (Exception ex) when (
            ex is NotSupportedException ||
            ex is InvalidDataException ||
            ex is System.Runtime.InteropServices.COMException)
        {
            // Cache is bad: purge it and allow a re-download
            _cachedImages.TryRemove(hash, out _);
            try { if (path.FileExists()) File.Delete(path.ToString()); } catch { }
            return (false, null);
        }
        finally
        {
            await imageStream.DisposeAsync();
        }
    }

    public async Task<bool> AddBytes(string url, byte[] bytes)
    {
        var hash = await UTF8.GetBytes(url + "|pngcache-v2").Hash();

        var gate = LockFor(hash);
        await gate.WaitAsync();
        try
        {
            if (_cachedImages.TryGetValue(hash, out _))
                return true;

            var img = UIUtils.BitmapImageFromStream(new MemoryStream(bytes, writable: false));
            _cachedImages[hash] = new CachedImage(img);

            await SaveImageAtomic(hash, bytes);
            return true;
        }
        finally
        {
            gate.Release();
        }
    }
}

public class CachedImage(BitmapImage image)
{
    private readonly DateTime _cachedAt = DateTime.Now;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);
    
    public BitmapImage Image { get; } = image;

    public bool IsExpired() => DateTime.Now - _cachedAt > _cacheDuration;
}