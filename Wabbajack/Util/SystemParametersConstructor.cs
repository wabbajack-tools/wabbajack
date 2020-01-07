using MahApps.Metro.Controls;
using Wabbajack.Lib;

namespace Wabbajack.Util
{
    public static class SystemParametersConstructor
    {
        public static SystemParameters Create()
        {
            return new SystemParameters
            {
                ScreenWidth = (int)System.Windows.SystemParameters.PrimaryScreenWidth,
                ScreenHeight = (int)System.Windows.SystemParameters.PrimaryScreenHeight
            };
        }
    }
}
