using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Wabbajack.Installer;

namespace Wabbajack.App.Avalonia.Util;

/// <summary>
///     The screen size and memory the installer writes into MO2 and ENB settings, measured without Windows APIs:
///     the screen from Avalonia, memory from the runtime and <c>/proc/meminfo</c>, video memory from the DRM
///     driver's sysfs node where it publishes one (amdgpu and Intel's xe do; NVIDIA's driver does not).
/// </summary>
public static class PortableSystemParameters
{
    /// <summary>Null when there is no screen to measure, which leaves the list's own settings alone.</summary>
    public static SystemParameters? Create()
    {
        var window = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        var screen = window?.Screens.Primary;
        if (screen == null) return null;

        var meminfo = ReadOrEmpty("/proc/meminfo");
        return new SystemParameters
        {
            // Bounds are in physical pixels, which is what the game's resolution is.
            ScreenWidth = screen.Bounds.Width,
            ScreenHeight = screen.Bounds.Height,
            SystemMemorySize = MemInfoBytes(meminfo, "MemTotal") ?? GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,
            SystemPageSize = MemInfoBytes(meminfo, "SwapTotal") ?? 0,
            VideoMemorySize = LargestVramBytes(),
            GpuName = ""
        };
    }

    /// <summary>A <c>/proc/meminfo</c> field in bytes, or null when it is not there.</summary>
    public static long? MemInfoBytes(string meminfo, string field)
    {
        foreach (var line in meminfo.Split('\n'))
        {
            var colon = line.IndexOf(':');
            if (colon < 0 || !line.AsSpan(0, colon).Trim().SequenceEqual(field)) continue;

            var value = line[(colon + 1)..].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (value.Length == 0 || !long.TryParse(value[0], out var amount)) return null;
            return value.Length > 1 && value[1].Equals("kB", StringComparison.OrdinalIgnoreCase) ? amount * 1024 : amount;
        }

        return null;
    }

    private static long LargestVramBytes()
    {
        try
        {
            if (!Directory.Exists("/sys/class/drm")) return 0;
            return Directory.EnumerateDirectories("/sys/class/drm", "card*")
                .Select(card => ReadOrEmpty(Path.Combine(card, "device", "mem_info_vram_total")).Trim())
                .Select(text => long.TryParse(text, out var bytes) ? bytes : 0)
                .DefaultIfEmpty(0)
                .Max();
        }
        catch (Exception)
        {
            return 0;
        }
    }

    private static string ReadOrEmpty(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : "";
        }
        catch (Exception)
        {
            return "";
        }
    }
}
