using System;
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
        }
    }
}