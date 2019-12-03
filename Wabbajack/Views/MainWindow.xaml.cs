using System;
using System.ComponentModel;
using System.Windows;
using MahApps.Metro.Controls;
using Wabbajack.Common;
using Application = System.Windows.Application;

namespace Wabbajack
{
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        private MainWindowVM _mwvm;
        private MainSettings _settings;

        internal bool ExitWhenClosing = true;

        public MainWindow()
        {
            // Wire any unhandled crashing exceptions to log before exiting
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                // Don't do any special logging side effects
                Wabbajack.Common.Utils.Log("Uncaught error:");
                Wabbajack.Common.Utils.Log(((Exception)e.ExceptionObject).ExceptionToString());
            };

            var appPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            if (!ExtensionManager.IsAssociated() || ExtensionManager.NeedsUpdating(appPath))
            {
                ExtensionManager.Associate(appPath);
            }

            Wabbajack.Common.Utils.Log($"Wabbajack Build - {ThisAssembly.Git.Sha}");

            // Load settings
            string[] args = Environment.GetCommandLineArgs();
            if ((args.Length > 1 && args[1] == "nosettings")
                || !MainSettings.TryLoadTypicalSettings(out var settings))
            {
                _settings = new MainSettings();
                RunWhenLoaded(DefaultSettings);
            }
            else
            {
                _settings = settings;
                RunWhenLoaded(LoadSettings);
            }

            // Set datacontext
            _mwvm = new MainWindowVM(this, _settings);
            DataContext = _mwvm;
        }

        public void Init(MainWindowVM vm, MainSettings settings)
        {
            DataContext = vm;
            _mwvm = vm;
            _settings = settings;
        }

        private void RunWhenLoaded(Action a)
        {
            if (IsLoaded)
            {
                a();
            }
            else
            {
                this.Loaded += (sender, e) =>
                {
                    a();
                };
            }
        }

        private void LoadSettings()
        {
            Width = _settings.Width;
            Height = _settings.Height;
            Left = _settings.PosX;
            Top = _settings.PosY;
        }

        private void DefaultSettings()
        {
            Width = 1300;
            Height = 960;
            Left = 15;
            Top = 15;
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            _mwvm.Dispose();
            _settings.PosX = Left;
            _settings.PosY = Top;
            _settings.Width = Width;
            _settings.Height = Height;
            MainSettings.SaveSettings(_settings);
            if (ExitWhenClosing)
            {
                Application.Current.Shutdown();
            }
        }
    }
}
