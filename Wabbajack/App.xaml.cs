using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using MahApps.Metro;
using Wabbajack.Common;

namespace Wabbajack
{
    /// <summary>
    ///     Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            // Wire any unhandled crashing exceptions to log before exiting
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                // Don't do any special logging side effects
                Utils.Log("Uncaught error:");
                Utils.Log(((Exception)e.ExceptionObject).ExceptionToString());
            };

            var appPath = Assembly.GetExecutingAssembly().Location;
            if (!ExtensionManager.IsAssociated() || ExtensionManager.NeedsUpdating(appPath))
            {
                ExtensionManager.Associate(appPath);
            }
        }
    }
}
