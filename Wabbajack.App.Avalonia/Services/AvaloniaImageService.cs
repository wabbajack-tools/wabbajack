using System;
using System.IO;
using System.Reactive.Linq;
using Avalonia.Media.Imaging;
using Wabbajack.Models;

namespace Wabbajack;

public sealed class AvaloniaImageService : IImageService
{
    public IObservable<object?> DownloadImage(IObservable<string?> urls, Action<Exception> onError, LoadingLock loadingLock)
        => urls.SelectMany(url => Observable.FromAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(url)) return (object?) null;
            using var ll = loadingLock.WithLoading();
            try
            {
                using var http = new System.Net.Http.HttpClient();
                await using var stream = await http.GetStreamAsync(url);
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                ms.Position = 0;
                return (object?) new Bitmap(ms);
            }
            catch (Exception ex) { onError(ex); return null; }
        }));

    public object? FromStream(Stream stream)
    {
        if (stream.CanSeek) stream.Position = 0;
        return new Bitmap(stream);
    }
}
