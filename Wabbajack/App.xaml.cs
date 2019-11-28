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

        protected override void OnStartup(StartupEventArgs e)
        {
            // add custom accent and theme resource dictionaries to the ThemeManager
            // you should replace MahAppsMetroThemesSample with your application name
            // and correct place where your custom accent lives
            ThemeManager.AddAccent("MahappsOverride", new Uri("pack://application:,,,/Wabbajack;component/Themes/MahappStyleOverride.xaml"));

            // get the current app style (theme and accent) from the application
            Tuple<AppTheme, Accent> theme = ThemeManager.DetectAppStyle(Application.Current);

            //// now change app style to the custom accent and current theme
            //ThemeManager.ChangeAppStyle(Application.Current,
            //                            ThemeManager.GetAccent("MahappsOverride"),
            //                            ThemeManager.AppThemes.First(x => x.Name.Equals("BaseDark")));

            base.OnStartup(e);
        }
    }
}
