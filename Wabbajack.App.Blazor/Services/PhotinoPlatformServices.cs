using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using Photino.NET;
using Wabbajack.Paths;

namespace Wabbajack.Blazor.Services;

// Holds the Photino window so the platform services can show native dialogs. Populated after the
// PhotinoBlazorApp is built (the window doesn't exist at DI-registration time).
public sealed class PhotinoWindowHolder
{
    public PhotinoWindow? Window { get; set; }
}

// Real file/folder picker using Photino's native dialogs (replaces the test stub). Without this, every
// Browse button (install/download paths, modlist file, upload) silently did nothing.
public sealed class PhotinoFileSelector : IFileSelector
{
    private readonly PhotinoWindowHolder _holder;
    public PhotinoFileSelector(PhotinoWindowHolder holder) => _holder = holder;

    public AbsolutePath? SelectPath(FileSelectorRequest request)
    {
        var window = _holder.Window;
        if (window is null) return null;

        var initialDir = request.InitialDirectory == default ? null : request.InitialDirectory.ToString();
        // Photino's pickers are async; SelectPath is sync. The native dialog is modal and runs on this
        // (UI) thread, so blocking on it here is fine.
        string[]? picked;
        if (request.IsFolderPicker)
        {
            picked = window.ShowOpenFolderAsync(request.Title ?? "Select a folder", initialDir, multiSelect: false)
                .GetAwaiter().GetResult();
        }
        else
        {
            var filters = request.Filters
                .Select(f => (f.Description, f.Patterns.ToArray()))
                .ToArray();
            picked = window.ShowOpenFileAsync(request.Title ?? "Select a file", initialDir, multiSelect: false, filters)
                .GetAwaiter().GetResult();
        }

        var path = picked?.FirstOrDefault();
        return string.IsNullOrEmpty(path) ? null : (AbsolutePath)path;
    }
}

// Native message boxes (replaces the no-op stub).
public sealed class PhotinoDialogService : IDialogService
{
    private readonly PhotinoWindowHolder _holder;
    public PhotinoDialogService(PhotinoWindowHolder holder) => _holder = holder;

    public void ShowError(string message, string title)
        => _holder.Window?.ShowMessage(title, message, PhotinoDialogButtons.Ok, PhotinoDialogIcon.Warning);

    public Task<bool> ShowConfirmation(string title, string message)
    {
        var window = _holder.Window;
        if (window is null) return Task.FromResult(false);
        var result = window.ShowMessage(title, message, PhotinoDialogButtons.YesNo, PhotinoDialogIcon.Question);
        return Task.FromResult(result == PhotinoDialogResult.Yes);
    }
}

// Clipboard via the webview (replaces the no-op stub) so "Copy URL" works.
public sealed class PhotinoClipboardService : IClipboardService
{
    private readonly IJSRuntime _js;
    public PhotinoClipboardService(IJSRuntime js) => _js = js;

    public async Task SetTextAsync(string text)
    {
        try { await _js.InvokeVoidAsync("navigator.clipboard.writeText", text); }
        catch { /* clipboard API may be unavailable; non-fatal */ }
    }
}
