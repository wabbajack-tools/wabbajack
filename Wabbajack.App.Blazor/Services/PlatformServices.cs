using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Wabbajack.Installer;
using Wabbajack.Models;
using Wabbajack.Paths;

namespace Wabbajack.Blazor.Services;

// The Blazor view binds <img> straight to the public modlist image URL (ImageUris.GetSmallImageUri),
// so the VM's opaque `object Image` is never used. We just satisfy the contract: yield null so VM
// construction/activation succeeds and the rest of the reactive pipeline runs unchanged.
public sealed class BlazorImageService : IImageService
{
    public IObservable<object?> DownloadImage(IObservable<string?> urls, Action<Exception> onError, LoadingLock loadingLock)
        => Observable.Return<object?>(null);

    public object? FromStream(System.IO.Stream stream) => null;
}

// Gallery never opens a dialog; log to console if it somehow does.
public sealed class StubDialogService : IDialogService
{
    public void ShowError(string message, string title) => Console.Error.WriteLine($"[dialog:{title}] {message}");
    public Task<bool> ShowConfirmation(string title, string message) => Task.FromResult(false);
}

// Gallery's "install from disk" path is out of spike scope; return cancelled.
public sealed class StubFileSelector : IFileSelector
{
    public AbsolutePath? SelectPath(FileSelectorRequest request) => null;
}

public sealed class StubClipboardService : IClipboardService
{
    public Task SetTextAsync(string text) => Task.CompletedTask;
}

// Mirrors AvaloniaSystemParameters: sane non-zero hints; unused by the gallery but needed if any
// VM in the graph asks for it.
public sealed class BlazorSystemParameters : ISystemParameters
{
    public SystemParameters Create()
    {
        var memInfo = GC.GetGCMemoryInfo();
        var systemMemory = memInfo.TotalAvailableMemoryBytes > 0
            ? memInfo.TotalAvailableMemoryBytes
            : 8L * 1024 * 1024 * 1024;

        return new SystemParameters
        {
            ScreenWidth = 1920,
            ScreenHeight = 1080,
            VideoMemorySize = 4L * 1024 * 1024 * 1024,
            SystemMemorySize = systemMemory,
            SystemPageSize = systemMemory,
            GpuName = ""
        };
    }
}
