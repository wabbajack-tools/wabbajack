using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using PInvoke;
using Silk.NET.Core.Native;
using Silk.NET.DXGI;
using Wabbajack.Installer;
using static PInvoke.User32;
using UnmanagedType = System.Runtime.InteropServices.UnmanagedType;

namespace Wabbajack.Util
{
    // Much of the GDI code here is taken from : https://github.com/ModOrganizer2/modorganizer/blob/master/src/envmetrics.cpp
    // Thanks to MO2 for being good citizens and supporting OSS code
    public class SystemParametersConstructor
    {
        private readonly ILogger<SystemParametersConstructor> _logger;

        public SystemParametersConstructor(ILogger<SystemParametersConstructor> logger)
        {
            _logger = logger;
        }
        private IEnumerable<(int Width, int Height, bool IsPrimary)> GetDisplays()
        {
            // Needed to make sure we get the right values from this call
            SetProcessDPIAware();
            unsafe
            {

                var col = new List<(int Width, int Height, bool IsPrimary)>();

                EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, 
                    delegate(IntPtr hMonitor, IntPtr hdcMonitor, RECT* lprcMonitor, void *dwData)
                    {
                        MONITORINFOEX mi = new MONITORINFOEX();
                        mi.cbSize = Marshal.SizeOf(mi);
                        bool success = GetMonitorInfo(hMonitor, (MONITORINFO*)&mi);
                        if (success)
                        {
                            col.Add(((mi.Monitor.right - mi.Monitor.left), (mi.Monitor.bottom - mi.Monitor.top),  mi.Flags == MONITORINFO_Flags.MONITORINFOF_PRIMARY));
                        }

                        return true;
                    }, IntPtr.Zero);
                return col;
            }
        }
        
        public SystemParameters Create()
        {
            var (width, height, _) = GetDisplays().First(d => d.IsPrimary);

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
                    
                    uint i = 0u;
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
                        
                        var systemMemory = (ulong) adapterDesc.DedicatedSystemMemory;
                        var videoMemory = (ulong) adapterDesc.DedicatedVideoMemory;
                        
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
            var gpuName = GetGPUName();
            return new SystemParameters
            {
                ScreenWidth = width,
                ScreenHeight = height,
                VideoMemorySize = (long)dxgiMemory,
                SystemMemorySize = (long)memory.ullTotalPhys,
                SystemPageSize = (long)memory.ullTotalPageFile - (long)memory.ullTotalPhys,
                GpuName = gpuName
            };
        }

        private string GetGPUName()
        {
            HashSet<string> gpuManufacturers = ["amd", "intel", "nvidia"];
            string gpuName = "";
            uint gpuRefreshRate = 0;
            
            try
            {
                ManagementObjectSearcher videoControllers = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
            
                foreach (ManagementObject obj in videoControllers.Get())
                {
                    if (obj["CurrentRefreshRate"] != null && obj["Description"] != null)
                    {
                        var currentRefreshRate = (uint)obj["CurrentRefreshRate"];
                        var currentName = obj["Description"].ToString();
                        
                        if (gpuManufacturers.Any(s => currentName.Contains(s, StringComparison.OrdinalIgnoreCase)) && currentRefreshRate > gpuRefreshRate)
                        {
                            gpuName = currentName;
                            gpuRefreshRate = currentRefreshRate;
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                _logger.LogError("Failed to get GPU information: {ex}", ex.ToString());
            }

            return gpuName;
        }

        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

        public static MEMORYSTATUSEX GetMemoryStatus()
        {
            var mstat = new MEMORYSTATUSEX();
            GlobalMemoryStatusEx(mstat);
            return mstat;
        }
        
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public class MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
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
}
