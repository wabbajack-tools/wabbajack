using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Reactive.Linq;
using Avalonia.Media.Imaging;
using Microsoft.Extensions.Logging;
using Wabbajack.App.Avalonia.Models;

namespace Wabbajack.App.Avalonia.Util;

/// <summary>
/// Loads the game icons referenced by <see cref="Wabbajack.DTOs.GameMetaData.IconSource" /> off the UI thread:
/// one shared download per URL, so the hundreds of tiles showing the same game reuse a single request and bitmap.
/// </summary>
public class GameIconCache(ILogger<GameIconCache> logger, HttpClient client, ImageCacheManager imageCache)
{
    private readonly ConcurrentDictionary<string, IObservable<Bitmap?>> _icons = new();
    private readonly LoadingLock _loadingLock = new();

    public IObservable<Bitmap?> Get(string iconSource)
    {
        if (!Uri.TryCreate(iconSource, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return Observable.Return<Bitmap?>(null);

        return _icons.GetOrAdd(iconSource, url => Observable.Return(url)
            .DownloadBitmap(
                ex => logger.LogError("Error downloading game icon from {IconSource}: {Exception}", url, ex.ToString()),
                _loadingLock, client, imageCache)
            .Replay(1)
            .RefCount());
    }
}
