using System;

namespace Wabbajack.Lib
{
    public class SystemParameters
    {
        private static long ToMB(long input)
        {
            //              KB     MB   
            return input / 1024 / 1024;
        }
        
        public int ScreenHeight { get; set; }
        public int ScreenWidth { get; set; }
        public long VideoMemorySize { get; set; }
        public long SystemMemorySize { get; set; }

        public long SystemPageSize { get; set; }

        public Version WindowsVersion { get; set; } = Environment.OSVersion.Version;

        /// <summary>
        /// Value used in LE ENBs for VideoMemorySizeMb
        /// </summary>
        public long EnbLEVRAMSize => Math.Min(ToMB(SystemMemorySize) + ToMB(VideoMemorySize), 10240);
    }
}
