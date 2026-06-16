using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Wabbajack.Messages;

namespace Wabbajack.Services;

/// <summary>
/// Windows-only helper that drives the taskbar button progress indicator via the
/// <c>ITaskbarList3</c> COM interface. All methods are no-ops on non-Windows platforms
/// so the Avalonia app stays cross-platform.
/// </summary>
public static class WindowsTaskbarProgress
{
    // TBPFLAG values per the Windows shell API.
    [Flags]
    public enum TBPFLAG
    {
        TBPF_NOPROGRESS = 0,
        TBPF_INDETERMINATE = 1,
        TBPF_NORMAL = 2,
        TBPF_ERROR = 4,
        TBPF_PAUSED = 8
    }

    [ComImport]
    [Guid("ea1afb91-9e28-4b86-90e9-9e9f8a5eefaf")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ITaskbarList3
    {
        // ITaskbarList
        void HrInit();
        void AddTab(IntPtr hwnd);
        void DeleteTab(IntPtr hwnd);
        void ActivateTab(IntPtr hwnd);
        void SetActiveAlt(IntPtr hwnd);

        // ITaskbarList2
        void MarkFullscreenWindow(IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] bool fFullscreen);

        // ITaskbarList3
        void SetProgressValue(IntPtr hwnd, ulong ullCompleted, ulong ullTotal);
        void SetProgressState(IntPtr hwnd, TBPFLAG tbpFlags);
        // The remaining ITaskbarList3 members are not used; declaring the ones above
        // keeps the vtable layout correct for the calls we make.
    }

    [ComImport]
    [Guid("56FDF344-FD6D-11d0-958A-006097C9A090")]
    [ClassInterface(ClassInterfaceType.None)]
    private class TaskbarInstance
    {
    }

    private static readonly object _lock = new();
    private static ITaskbarList3? _taskbar;
    private static bool _failed;

    [SupportedOSPlatform("windows")]
    private static ITaskbarList3? GetInstance()
    {
        if (_failed) return null;
        if (_taskbar != null) return _taskbar;

        lock (_lock)
        {
            if (_failed) return null;
            if (_taskbar != null) return _taskbar;
            try
            {
                var instance = (ITaskbarList3)new TaskbarInstance();
                instance.HrInit();
                _taskbar = instance;
            }
            catch
            {
                _failed = true;
            }
            return _taskbar;
        }
    }

    private static TBPFLAG ToFlag(TaskbarItemState state) => state switch
    {
        TaskbarItemState.None => TBPFLAG.TBPF_NOPROGRESS,
        TaskbarItemState.Normal => TBPFLAG.TBPF_NORMAL,
        TaskbarItemState.Indeterminate => TBPFLAG.TBPF_INDETERMINATE,
        TaskbarItemState.Error => TBPFLAG.TBPF_ERROR,
        TaskbarItemState.Paused => TBPFLAG.TBPF_PAUSED,
        _ => TBPFLAG.TBPF_NOPROGRESS
    };

    /// <summary>Sets the taskbar progress state for the given window handle.</summary>
    public static void SetState(IntPtr hwnd, TBPFLAG state)
    {
        if (!OperatingSystem.IsWindows()) return;
        if (hwnd == IntPtr.Zero) return;
        var taskbar = GetInstance();
        taskbar?.SetProgressState(hwnd, state);
    }

    /// <summary>Sets the taskbar progress value (completed / total) for the given window handle.</summary>
    public static void SetValue(IntPtr hwnd, ulong completed, ulong total)
    {
        if (!OperatingSystem.IsWindows()) return;
        if (hwnd == IntPtr.Zero) return;
        if (total == 0) return;
        var taskbar = GetInstance();
        taskbar?.SetProgressValue(hwnd, completed, total);
    }

    /// <summary>
    /// Convenience entry point: maps a <see cref="TaskbarItemState"/> plus a progress fraction
    /// (0.0 - 1.0) onto the taskbar button. A no-op on non-Windows.
    /// </summary>
    public static void Update(IntPtr hwnd, TaskbarItemState state, double progress)
    {
        if (!OperatingSystem.IsWindows()) return;
        if (hwnd == IntPtr.Zero) return;

        var flag = ToFlag(state);
        SetState(hwnd, flag);

        // Only a determinate "Normal"/"Paused"/"Error" bar shows a value; for those, push the fraction.
        if (flag is TBPFLAG.TBPF_NORMAL or TBPFLAG.TBPF_PAUSED or TBPFLAG.TBPF_ERROR)
        {
            var clamped = Math.Clamp(progress, 0.0, 1.0);
            SetValue(hwnd, (ulong)Math.Round(clamped * 1000), 1000);
        }
    }
}
