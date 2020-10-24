using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using MahApps.Metro.Controls;
using Microsoft.VisualBasic;
using PInvoke;
using SharpDX.DXGI;
using Wabbajack.Lib;
using static PInvoke.User32;
using static PInvoke.Gdi32;

namespace Wabbajack.Util
{
    // Much of the GDI code here is taken from : https://github.com/ModOrganizer2/modorganizer/blob/master/src/envmetrics.cpp
    // Thanks to MO2 for being good citizens and supporting OSS code
    public static class SystemParametersConstructor
    {
        private static List<(int Width, int Height, bool IsPrimary)> GetDisplays()
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
        
        public static SystemParameters Create()
        {
            var (width, height, _) = GetDisplays().First(d => d.IsPrimary);
            
            using var f = new Factory1();
            var video_memory = f.Adapters1.Select(a =>
                Math.Max(a.Description.DedicatedSystemMemory, (long)a.Description.DedicatedVideoMemory)).Max();
            var memory = Common.Utils.GetMemoryStatus();
            return new SystemParameters
            {
                ScreenWidth = width,
                ScreenHeight = height,
                VideoMemorySize = video_memory,
                SystemMemorySize = (long)memory.ullTotalPhys,
            };
        }
    }
}
