using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using PInvoke;
using Silk.NET.Core.Native;
using Silk.NET.DXGI;
using Wabbajack.Common;
using Wabbajack.Installer;
using Wabbajack.Paths.IO;
using static PInvoke.User32;
using UnmanagedType = System.Runtime.InteropServices.UnmanagedType;

namespace Wabbajack.App.Blazor.Utility;

// Much of the GDI code here is taken from : https://github.com/ModOrganizer2/modorganizer/blob/master/src/envmetrics.cpp
// Thanks to MO2 for being good citizens and supporting OSS code
public class SystemParametersConstructor
{
    private readonly ILogger<SystemParametersConstructor> _logger;

    public SystemParametersConstructor(ILogger<SystemParametersConstructor> logger)
    {
        _logger = logger;
        
        _logger.LogInformation("Wabbajack Build - {Sha}", ThisAssembly.Git.Sha);
        _logger.LogInformation("Running in {EntryPoint}", KnownFolders.EntryPoint);
        
        _logger.LogInformation("Detected Windows Version: {Version}", Environment.OSVersion.VersionString);

    }

    private IEnumerable<(int Width, int Height, bool IsPrimary)> GetDisplays()
    {
        // Needed to make sure we get the right values from this call
        SetProcessDPIAware();
        unsafe
        {
            var col = new List<(int Width, int Height, bool IsPrimary)>();

            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
                delegate(IntPtr hMonitor, IntPtr hdcMonitor, RECT* lprcMonitor, void* dwData)
                {
                    var mi = new MONITORINFOEX();
                    mi.cbSize = Marshal.SizeOf(mi);
                    var success = GetMonitorInfo(hMonitor, (MONITORINFO*)&mi);
                    if (success)
                        col.Add((mi.Monitor.right - mi.Monitor.left, mi.Monitor.bottom - mi.Monitor.top,
                            mi.Flags == MONITORINFO_Flags.MONITORINFOF_PRIMARY));

                    return true;
                }, IntPtr.Zero);
            return col;
        }
    }

    public SystemParameters Create()
    {
        (var width, var height, _) = GetDisplays().First(d => d.IsPrimary);

        /*using var f = new SharpDX.DXGI.Factory1();
        var video_memory = f.Adapters1.Select(a =>
            Math.Max(a.Description.DedicatedSystemMemory, (long)a.Description.DedicatedVideoMemory)).Max();*/

        var dxgiMemory = 0UL;

        unsafe
        {
            using var api = DXGI.GetApi();

            IDXGIFactory1* factory1 = default;

            try
            {
                //https://docs.microsoft.com/en-us/windows/win32/api/dxgi/nf-dxgi-createdxgifactory1
                SilkMarshal.ThrowHResult(api.CreateDXGIFactory1(SilkMarshal.GuidPtrOf<IDXGIFactory1>(), (void**)&factory1));

                var i = 0u;
                while (true)
                {
                    IDXGIAdapter1* adapter1 = default;

                    //https://docs.microsoft.com/en-us/windows/win32/api/dxgi/nf-dxgi-idxgifactory1-enumadapters1
                    var res = factory1->EnumAdapters1(i, &adapter1);

                    var exception = Marshal.GetExceptionForHR(res);
                    if (exception != null) break;

                    AdapterDesc1 adapterDesc = default;

                    //https://docs.microsoft.com/en-us/windows/win32/api/dxgi/nf-dxgi-idxgiadapter1-getdesc1
                    SilkMarshal.ThrowHResult(adapter1->GetDesc1(&adapterDesc));

                    var systemMemory = (ulong)adapterDesc.DedicatedSystemMemory;
                    var videoMemory = (ulong)adapterDesc.DedicatedVideoMemory;

                    var maxMemory = Math.Max(systemMemory, videoMemory);
                    if (maxMemory > dxgiMemory)
                        dxgiMemory = maxMemory;

                    adapter1->Release();
                    i++;
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "While getting SystemParameters");
            }
            finally
            {
                if (factory1->LpVtbl != (void**)IntPtr.Zero)
                    factory1->Release();
            }
        }

        var memory = GetMemoryStatus();
        var p = new SystemParameters
        {
            ScreenWidth      = width,
            ScreenHeight     = height,
            VideoMemorySize  = (long)dxgiMemory,
            SystemMemorySize = (long)memory.ullTotalPhys,
            SystemPageSize   = (long)memory.ullTotalPageFile - (long)memory.ullTotalPhys
        };

        _logger.LogInformation(
            "System settings - ({MemorySize} RAM) ({PageSize} Page), Display: {ScreenWidth} x {ScreenHeight} ({Vram} VRAM - VideoMemorySizeMb={ENBVRam})",
            p.SystemMemorySize.ToFileSizeString(), p.SystemPageSize.ToFileSizeString(), p.ScreenWidth, p.ScreenHeight,
            p.VideoMemorySize.ToFileSizeString(), p.EnbLEVRAMSize);

        if (p.SystemPageSize == 0)
            _logger.LogInformation(
                "Page file is disabled! Consider increasing to 20000MB. A disabled page file can cause crashes and poor in-game performance");
        else if (p.SystemPageSize < 2e+10)
            _logger.LogInformation(
                "Page file below recommended! Consider increasing to 20000MB. A suboptimal page file can cause crashes and poor in-game performance");
        
        return p;
    }

    [return: MarshalAs(UnmanagedType.Bool)]
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx([In] [Out] MEMORYSTATUSEX lpBuffer);

    public static MEMORYSTATUSEX GetMemoryStatus()
    {
        var mstat = new MEMORYSTATUSEX();
        GlobalMemoryStatusEx(mstat);
        return mstat;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public class MEMORYSTATUSEX
    {
        public uint  dwLength;
        public uint  dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;

        public MEMORYSTATUSEX()
        {
            dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        }
    }
}
