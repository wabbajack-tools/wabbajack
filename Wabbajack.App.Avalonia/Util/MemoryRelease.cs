using System;
using System.Runtime;

namespace Wabbajack.App.Avalonia.Util;

/// <summary>
///     Hands memory back after an install or a compile. Both index every file of every archive they touch, and
///     once that is unreferenced the GC frees it but keeps the pages, so the app still looked several gigabytes
///     big while doing nothing. One aggressive, compacting collection at the end of the run returns them.
/// </summary>
public static class MemoryRelease
{
    public static void AfterLargeRun()
    {
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
    }
}
