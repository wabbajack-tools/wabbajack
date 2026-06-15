using System;
using System.IO;
using Wabbajack.Models;

namespace Wabbajack;

/// <summary>
/// Platform abstraction for loading images, so ViewModels can hold an opaque platform image
/// (<see cref="object"/>) instead of WPF's BitmapImage/BitmapFrame. The WPF app returns a
/// BitmapImage; an Avalonia app would return its own Bitmap. The returned object binds directly to
/// an image control's source.
/// </summary>
public interface IImageService
{
    /// <summary>
    /// Downloads, decodes and caches images for the given stream of URLs, returning the platform
    /// image (or null on failure).
    /// </summary>
    IObservable<object?> DownloadImage(IObservable<string?> urls, Action<Exception> onError, LoadingLock loadingLock);

    /// <summary>Decodes a stream into a platform image.</summary>
    object? FromStream(Stream stream);
}
