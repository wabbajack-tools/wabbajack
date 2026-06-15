using System;
using System.IO;
using System.Net.Http;
using System.Reactive.Linq;
using Wabbajack.Models;

namespace Wabbajack;

/// <summary>
/// WPF implementation of <see cref="IImageService"/>: produces BitmapImage instances via the
/// existing UIUtils image pipeline, returned as opaque <see cref="object"/> so ViewModels stay
/// free of WPF image types.
/// </summary>
public sealed class WpfImageService : IImageService
{
    private readonly HttpClient _httpClient;
    private readonly ImageCacheManager _icm;

    public WpfImageService(HttpClient httpClient, ImageCacheManager icm)
    {
        _httpClient = httpClient;
        _icm = icm;
    }

    public IObservable<object?> DownloadImage(IObservable<string?> urls, Action<Exception> onError, LoadingLock loadingLock) =>
        urls.Select(u => u!)
            .DownloadBitmapImage(onError, loadingLock, _httpClient, _icm)
            .Select(img => (object?) img);

    public object? FromStream(Stream stream) => UIUtils.BitmapImageFromStream(stream);
}
