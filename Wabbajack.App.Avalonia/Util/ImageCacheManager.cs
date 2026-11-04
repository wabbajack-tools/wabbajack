using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using DynamicData.Kernel;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using static System.Text.Encoding;

namespace Wabbajack.App.Avalonia.Util;

/// <summary>
/// The WPF app's image cache, holding Avalonia bitmaps. Same folder and the same file names (xxHash64 of the
/// URL plus "|pngcache-v2"), so the two apps share what either has downloaded. Decoded images stay in memory
/// for five minutes, and their files are deleted when they expire, as before.
/// </summary>
public class ImageCacheManager
{
    private readonly TimeSpan _pollInterval = TimeSpan.FromMinutes(1);
    private readonly ILogger<ImageCacheManager> _logger;
    private readonly ConcurrentDictionary<Hash, SemaphoreSlim> _writeLocks = new();
    private readonly AbsolutePath _imageCachePath;
    private readonly ConcurrentDictionary<Hash, CachedImage> _cachedImages = new();

    public ImageCacheManager(ILogger<ImageCacheManager> logger, Wabbajack.Services.OSIntegrated.Configuration configuration)
    {
        _logger = logger;
        _imageCachePath = configuration.ImageCacheLocation;
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
                    if (!path.FileExists()) continue;
                    try { File.Delete(path.ToString()); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to delete cached image {hashHex}", hash.ToHex());
                }
            }
        });
    }

    private AbsolutePath PathFor(Hash hash) => _imageCachePath.Combine(hash.ToHex());

    private SemaphoreSlim LockFor(Hash hash) => _writeLocks.GetOrAdd(hash, _ => new SemaphoreSlim(1, 1));

    private static async Task<Hash> KeyFor(string url) => await UTF8.GetBytes(url + "|pngcache-v2").Hash();

    public async Task<(bool, Bitmap?)> Get(string url)
    {
        var hash = await KeyFor(url);
        if (_cachedImages.TryGetValue(hash, out var cachedImage))
            return (true, cachedImage.Image);

        var path = PathFor(hash);
        if (!path.FileExists()) return (false, null);

        try
        {
            var bytes = await File.ReadAllBytesAsync(path.ToString());

            // PNG signature: 89 50 4E 47 0D 0A 1A 0A. Anything else is an old WebP cache entry.
            if (bytes.Length < 8 || bytes[0] != 0x89 || bytes[1] != 0x50 || bytes[2] != 0x4E || bytes[3] != 0x47 ||
                bytes[4] != 0x0D || bytes[5] != 0x0A || bytes[6] != 0x1A || bytes[7] != 0x0A)
                throw new InvalidDataException("Cached image is not PNG");

            var img = UIUtils.BitmapFromStream(new MemoryStream(bytes, false));
            _cachedImages.TryAdd(hash, new CachedImage(img));
            return (true, img);
        }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or IOException or ArgumentException)
        {
            // A bad entry: drop it so the image is downloaded again.
            _cachedImages.TryRemove(hash, out _);
            try { if (path.FileExists()) File.Delete(path.ToString()); } catch { }
            return (false, null);
        }
    }

    public async Task<bool> AddBytes(string url, byte[] bytes)
    {
        var hash = await KeyFor(url);
        var gate = LockFor(hash);
        await gate.WaitAsync();
        try
        {
            if (_cachedImages.ContainsKey(hash)) return true;

            _cachedImages[hash] = new CachedImage(UIUtils.BitmapFromStream(new MemoryStream(bytes, false)));
            await SaveImageAtomic(hash, bytes);
            return true;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task SaveImageAtomic(Hash hash, byte[] bytes)
    {
        var path = PathFor(hash).ToString();
        var tmp = path + ".tmp";

        await using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await fs.WriteAsync(bytes);
            await fs.FlushAsync();
        }

        try
        {
            if (File.Exists(path)) File.Replace(tmp, path, null);
            else File.Move(tmp, path);
        }
        finally
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
        }
    }
}

public class CachedImage(Bitmap image)
{
    private readonly DateTime _cachedAt = DateTime.Now;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);

    public Bitmap Image { get; } = image;

    public bool IsExpired() => DateTime.Now - _cachedAt > _cacheDuration;
}
