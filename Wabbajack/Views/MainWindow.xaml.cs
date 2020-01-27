using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using MahApps.Metro.Controls;
using Wabbajack.Common;
using Wabbajack.Lib.LibCefHelpers;
using Application = System.Windows.Application;
using Utils = Wabbajack.Common.Utils;

namespace Wabbajack
{
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        private MainWindowVM _mwvm;
        private MainSettings _settings;

        public MainWindow()
        {
            Helpers.Init();
            // Wire any unhandled crashing exceptions to log before exiting
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                // Don't do any special logging side effects
                Utils.Error(((Exception)e.ExceptionObject), "Uncaught error");
            };

            Utils.Log($"Wabbajack Build - {ThisAssembly.Git.Sha}");

            // Run logic to associate wabbajack lists with this app in the background
            Task.Run(async () =>
            {
                var appPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                try
                {
                    if (!ModListAssociationManager.IsAssociated() || ModListAssociationManager.NeedsUpdating(appPath))
                    {
                        ModListAssociationManager.Associate(appPath);
                    }
                }
                catch (Exception e)
                {
                    Utils.Log($"ExtensionManager had an exception:\n{e}");
                }
            }).FireAndForget();

            // Load settings
            if (CLIArguments.NoSettings || !MainSettings.TryLoadTypicalSettings(out var settings))
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

            // Bring window to the front if it isn't already
            this.Initialized += (s, e) =>
            {
                this.Activate();
                this.Topmost = true;
                this.Focus();
            };
            this.ContentRendered += (s, e) =>
            {
                this.Topmost = false;
            };
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
            _mwvm.ShutdownApplication();
        }
    }
}
