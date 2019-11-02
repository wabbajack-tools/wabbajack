using System;
using System.Reflection;
using System.Windows;
using Wabbajack.Common;
using Wabbajack.Lib.Updater;

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
                Utils.SetLoggerFn((s) => { });
                Utils.Log("Uncaught error:");
                Utils.Log(((Exception)e.ExceptionObject).ExceptionToString());
            };

            var appPath = Assembly.GetExecutingAssembly().Location;
            if (!ExtensionManager.IsAssociated() || ExtensionManager.NeedsUpdating(appPath))
            {
                ExtensionManager.Associate(appPath);
            }

            string[] args = Environment.GetCommandLineArgs();
            StartupUri = new Uri("Views/ModeSelectionWindow.xaml", UriKind.Relative);
            if (args.Length != 3) return;
            if (!args[1].Contains("-i")) return;
            // modlists gets loaded using a shell command
            StartupUri = new Uri("Views/MainWindow.xaml", UriKind.Relative);
        }
    }
}