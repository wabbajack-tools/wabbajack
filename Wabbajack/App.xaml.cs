using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
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
