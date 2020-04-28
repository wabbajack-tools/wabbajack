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
            CLIOld.ParseOptions(Environment.GetCommandLineArgs());
            if (CLIArguments.Help)
                CLIOld.DisplayHelpText();
        }
    }
}
