using System;
using System.Windows;
using Wabbajack.Common;
using Wabbajack.Lib;

namespace Wabbajack
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            Consts.LogsFolder = LauncherUpdater.CommonFolder.Value.Combine("logs");
            Consts.LogsFolder.CreateDirectory();

            LoggingSettings.LogToFile = true;
            Utils.InitalizeLogging().Wait();

            CLIOld.ParseOptions(Environment.GetCommandLineArgs());
            if (CLIArguments.Help)
                CLIOld.DisplayHelpText();
        }
    }
}
