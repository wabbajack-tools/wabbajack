using System;
using System.Runtime.InteropServices;
using System.Windows;
using Wabbajack.Common;
using Wabbajack.Updater;

namespace Wabbajack
{
    /// <summary>
    ///     Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {

            Utils.Log($"Wabbajack Build - {ThisAssembly.Git.Sha}");
            SetupHandlers();

            var args = Environment.GetCommandLineArgs();
            if (args.Length > 1)
            {
                Utils.SetLoggerFn(f => {});
                WorkQueue.Init((a, b, c) => {}, (a, b) => {});
                var updater = new CheckForUpdates(args[1]);
                if (updater.FindOutdatedMods())
                {
                    Environment.Exit(0);
                }
                Environment.Exit(1);
            }

        }

        private void SetupHandlers()
        {
            AppDomain.CurrentDomain.UnhandledException += AppHandler;
        }

        private void AppHandler(object sender, UnhandledExceptionEventArgs e)
        {
            Utils.Log("Uncaught error:");
            Utils.Log(((Exception)e.ExceptionObject).ExceptionToString());
        }
    }
}