using System;
using Wabbajack.DTOs.DownloadStates;
using ManualState = Wabbajack.DTOs.DownloadStates.Manual;

namespace Wabbajack.Downloaders;

/// <summary>
/// Mega, GoogleDrive, MediaFire, ModDB, LoversLab, VectorPlexus and Bethesda no longer have
/// dedicated downloaders. Their download-state DTOs are retained so existing modlists still
/// deserialize; at dispatch time we reinterpret them as <see cref="Manual"/> downloads so the
/// user can fetch the file by hand. Returns null for states that still have a real downloader.
/// </summary>
public static class ManualReinterpreter
{
    public static ManualState? ToManual(IDownloadState state)
    {
        return state switch
        {
            Mega m => Make(m.Url, "Mega"),
            GoogleDrive g => Make(g.GetUri(), "Google Drive"),
            MediaFire mf => Make(mf.Url, "MediaFire"),
            ModDB md => Make(md.Url, "ModDB"),
            LoversLab ll => Make(Ips4Url(ll, "https://www.loverslab.com/"), $"LoversLab{Named(ll.Name)}"),
            VectorPlexus vp => Make(Ips4Url(vp, "https://vectorplexus.com/"), $"VectorPlexus{Named(vp.Name)}"),
            Bethesda b => Make(new Uri("https://bethesda.net/"), $"Bethesda Creation Club content \"{b.Name}\""),
            _ => null
        };
    }

    private static ManualState Make(Uri url, string source) =>
        new()
        {
            Url = url,
            Prompt = $"This file was previously downloaded automatically from {source}, which is no longer supported. Please download it manually."
        };

    private static string Named(string? name) => string.IsNullOrWhiteSpace(name) ? "" : $" ({name})";

    private static Uri Ips4Url(IPS4OAuth2 state, string fallback) =>
        Uri.TryCreate(state.IPS4Url, UriKind.Absolute, out var uri) ? uri : new Uri(fallback);
}
