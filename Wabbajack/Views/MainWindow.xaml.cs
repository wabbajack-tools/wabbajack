using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using MahApps.Metro.Controls;
using Newtonsoft.Json;
using Wabbajack.Common;
using Wabbajack.Common.StoreHandlers;
using Wabbajack.Lib.LibCefHelpers;
using Wabbajack.Util;
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
            try
            {
                // Wire any unhandled crashing exceptions to log before exiting
                AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
                {
                    // Don't do any special logging side effects
                    Utils.LogStraightToFile("Error.");
                    Utils.LogStraightToFile(((Exception)e.ExceptionObject).ToString());
                    Environment.Exit(-1);
                };

                Utils.Log($"Wabbajack Build - {ThisAssembly.Git.Sha}");
                Utils.Log($"Running in {AbsolutePath.EntryPoint}");

                var p = SystemParametersConstructor.Create();

                Utils.Log($"Detected Windows Version: {p.WindowsVersion}");

                if (!(p.WindowsVersion.Major >= 10 && p.WindowsVersion.Minor >= 0))
                    Utils.Log(
                        $"You are not running a recent version of Windows (version 10 or greater), Wabbajack is not supported on OS versions older than Windows 10.");

                Utils.Log(
                    $"System settings - ({p.SystemMemorySize.ToFileSizeString()} RAM) ({p.SystemPageSize.ToFileSizeString()} Page), Display: {p.ScreenWidth} x {p.ScreenHeight} ({p.VideoMemorySize.ToFileSizeString()} VRAM - VideoMemorySizeMb={p.EnbLEVRAMSize})");

                if (p.SystemPageSize == 0)
                    Utils.Log("Pagefile is disabled! Consider increasing to 20000MB. A disabled pagefile can cause crashes and poor in-game performance.");
                else if (p.SystemPageSize < 2e+10)
                    Utils.Log("Pagefile below recommended! Consider increasing to 20000MB. A suboptimal pagefile can cause crashes and poor in-game performance.");

                Warmup();

                var (settings, loadedSettings) = MainSettings.TryLoadTypicalSettings().AsTask().Result;
                // Load settings
                if (CLIArguments.NoSettings || !loadedSettings)
                {
                    _settings = new MainSettings {Version = Consts.SettingsVersion};
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
            catch (Exception ex)
            {
                Utils.LogStraightToFile("Error");
                Utils.LogStraightToFile(ex.ToString());
                Environment.Exit(-1);
            }
        }

        public void Init(MainWindowVM vm, MainSettings settings)
        {
            DataContext = vm;
            _mwvm = vm;
            _settings = settings;
        }

        /// <summary>
        /// Starts some background initialization tasks spinning so they're already prepped when actually needed
        /// </summary>
        private void Warmup()
        {
            TempFolder.Warmup();
            // ToDo
            // Currently this is a blocking call.  Perhaps upgrade to be run in a background task.
            // Would first need to ensure users of CEF properly await the background initialization before use
            Helpers.Init();
            StoreHandler.Warmup();

            Task.Run(AssociateListsWithWabbajack).FireAndForget();
        }

        /// <summary>
        /// Run logic to associate wabbajack lists with this app in the background
        /// </summary>
        private void AssociateListsWithWabbajack()
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
            _mwvm.ShutdownApplication().Wait();
        }
    }
}
