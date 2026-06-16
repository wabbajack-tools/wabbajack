using System;
using Wabbajack.Installer;

namespace Wabbajack;

/// <summary>
/// Cross-platform <see cref="ISystemParameters"/> implementation for the Avalonia app. Screen size and
/// video memory can't easily be queried in a headless/cross-platform context, so we use sane non-zero
/// defaults (the installer only uses these as hints). CPU count and system memory are read from the
/// runtime.
/// </summary>
public class AvaloniaSystemParameters : ISystemParameters
{
    public SystemParameters Create()
    {
        var memInfo = GC.GetGCMemoryInfo();
        var systemMemory = memInfo.TotalAvailableMemoryBytes > 0
            ? memInfo.TotalAvailableMemoryBytes
            : 8L * 1024 * 1024 * 1024; // 8 GB fallback

        return new SystemParameters
        {
            ScreenWidth = 1920,
            ScreenHeight = 1080,
            VideoMemorySize = 4L * 1024 * 1024 * 1024, // 4 GB
            SystemMemorySize = systemMemory,
            SystemPageSize = systemMemory, // approximate page file as system memory
            GpuName = ""
        };
    }
}
