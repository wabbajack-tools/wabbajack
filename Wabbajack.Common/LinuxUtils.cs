using System;

namespace Wabbajack.Common
{
    public static class LinuxUtils
    {
        public static AbsolutePath GetHomeFolder()
        {
            return new (Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        }
    }
}
