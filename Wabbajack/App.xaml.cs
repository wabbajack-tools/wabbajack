using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using MahApps.Metro;
using Wabbajack.Common;
using Wabbajack.Lib.LibCefHelpers;

namespace Wabbajack
{
    /// <summary>
    ///     Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            Helpers.ExtractLibs();
        }
    }
}
