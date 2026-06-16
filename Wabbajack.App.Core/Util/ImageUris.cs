using System;
using Wabbajack.DTOs;

namespace Wabbajack;

/// <summary>
/// Toolkit-agnostic helpers for building the modlist tile image URLs (and related display
/// formatting) shared by the WPF and Avalonia apps. These contain no WPF/Avalonia types, so they
/// can live in Core. WPF's <c>UIUtils</c> delegates to these to avoid duplicating the logic.
/// </summary>
public static class ImageUris
{
    public static string GetSmallImageUri(ModlistMetadata metadata)
    {
        var fileName = metadata.Links.MachineURL + "_small.webp";
        return $"https://raw.githubusercontent.com/wabbajack-tools/mod-lists/master/reports/{metadata.RepositoryName}/{fileName}";
    }

    public static string GetLargeImageUri(ModlistMetadata metadata)
    {
        var fileName = metadata.Links.MachineURL + "_large.webp";
        return $"https://raw.githubusercontent.com/wabbajack-tools/mod-lists/master/reports/{metadata.RepositoryName}/{fileName}";
    }

    /// <summary>
    /// Format bytes to a greater unit
    /// </summary>
    /// <param name="bytes">number of bytes</param>
    /// <returns></returns>
    public static string FormatBytes(long bytes, bool round = false)
    {
        string[] Suffix = { "B", "KB", "MB", "GB", "TB" };
        int i;
        double dblSByte = bytes;
        for (i = 0; i < Suffix.Length && bytes >= 1024; i++, bytes /= 1024)
        {
            dblSByte = bytes / 1024.0;
        }

        return String.Format("{0:0.##} {1}", round ? Math.Ceiling(dblSByte) : dblSByte, Suffix[i]);
    }
}
