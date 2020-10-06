using System;
using System.Windows;
using Wabbajack.Common;

namespace Wabbajack
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            LoggingSettings.LogToFile = true;

            CLIOld.ParseOptions(Environment.GetCommandLineArgs());
            if (CLIArguments.Help)
                CLIOld.DisplayHelpText();
        }
    }
}
