using System;

namespace Wabbajack.Installer;

public class SystemParameters
{
    public int ScreenWidth { get; set; }
    public int ScreenHeight { get; set; }

    public long VideoMemorySize { get; set; }
    public long SystemMemorySize { get; set; }

    public long SystemPageSize { get; set; }


    public string GpuName { get; set; }
    public long EnbLEVRAMSize => Math.Min(ToMB(SystemMemorySize) + ToMB(VideoMemorySize), 10240);

    private static long ToMB(long input)
    {
        //              KB     MB   
        return input / 1024 / 1024;
    }
}