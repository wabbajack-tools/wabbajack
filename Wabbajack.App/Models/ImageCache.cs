using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using SkiaSharp;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;

namespace Wabbajack.App.Models;

public class ImageCache
{
    private readonly Configuration _configuration;
    private readonly HttpClient _client;
    private readonly IResource<HttpClient> _limiter;

    public ImageCache(Configuration configuration, HttpClient client, IResource<HttpClient> limiter)
    {
        _configuration = configuration;
        _configuration.ImageCacheLocation.CreateDirectory();
        _client = client;
        _limiter = limiter;
    }

    public async Task<IBitmap> From(Uri uri, int width, int height)
    {
        var hash = (await Encoding.UTF8.GetBytes(uri.ToString()).Hash()).ToHex();
        var file = _configuration.ImageCacheLocation.Combine(hash + $"_{width}_{height}");
        
        if (!file.FileExists())
        {
            using var job = await _limiter.Begin("Loading Image", 0, CancellationToken.None);
            
            var wdata = await _client.GetByteArrayAsync(uri);
            var resized = SKBitmap.Decode(wdata).Resize(new SKSizeI(width, height), SKFilterQuality.High);
            await file.WriteAllBytesAsync(resized.Encode(SKEncodedImageFormat.Webp, 90).ToArray());
        }
        
        var data = await file.ReadAllBytesAsync();
        return new Bitmap(new MemoryStream(data));
    }

}