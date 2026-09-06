using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Reactive.Linq;
using System.Windows.Media.Imaging;

using Microsoft.Extensions.Logging;

using Wabbajack.Models;

namespace Wabbajack;

/// <summary>
/// Loads the game icons referenced by <see cref="Wabbajack.DTOs.GameMetaData.IconSource"/> off the UI thread.
/// </summary>
public class GameIconCache
{
    private readonly ILogger<GameIconCache> _logger;
    private readonly HttpClient _client;
    private readonly ImageCacheManager _imageCache;
    private readonly ConcurrentDictionary<string, IObservable<BitmapImage>> _icons = new();
    private readonly LoadingLock _loadingLock = new();

    public GameIconCache(ILogger<GameIconCache> logger, HttpClient client, ImageCacheManager imageCache)
    {
        _logger = logger;
        _client = client;
        _imageCache = imageCache;
    }

    /// <summary>
    /// One shared download per URL, so the hundreds of tiles showing the same game reuse a single request and the same bitmap.
    /// </summary>
    public IObservable<BitmapImage> Get(string iconSource)
    {
        if (!Uri.TryCreate(iconSource, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return Observable.Return<BitmapImage>(null);
        }

        return _icons.GetOrAdd(iconSource, url => Observable.Return(url)
            .DownloadBitmapImage(
                ex => _logger.LogError("Error downloading game icon from {IconSource}: {Exception}", url, ex.ToString()),
                _loadingLock, _client, _imageCache)
            .Replay(1)
            .RefCount());
    }
}
