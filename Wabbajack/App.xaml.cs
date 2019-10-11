using System;
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
            var args = Environment.GetCommandLineArgs();
            if (args.Length > 1)
            {
                Utils.SetLoggerFn(f => { });
                WorkQueue.Init((a, b, c) => { }, (a, b) => { });
                var updater = new CheckForUpdates(args[1]);
                if (updater.FindOutdatedMods())
                {
                    Environment.Exit(0);
                }
                Environment.Exit(1);
            }

        }
    }
}