using System;
using System.ComponentModel;
using System.Threading.Tasks;
using MahApps.Metro.Controls;
using Microsoft.Extensions.Logging;
using Wabbajack.Common;
using Wabbajack;
using Wabbajack.LibCefHelpers;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.Util;

namespace Wabbajack
{
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        private MainWindowVM _mwvm;
        private MainSettings _settings;
        private readonly ILogger<MainWindow> _logger;
        private readonly SystemParametersConstructor _systemParams;

        public MainWindow(ILogger<MainWindow> logger, SystemParametersConstructor systemParams, LauncherUpdater updater, MainWindowVM vm)
        {
            InitializeComponent();
            _mwvm = vm;
            DataContext = _mwvm;
            
            _logger = logger;
            _systemParams = systemParams;
            try
            {
                // Wire any unhandled crashing exceptions to log before exiting
                AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
                {
                    // Don't do any special logging side effects
                    _logger.LogError((Exception)e.ExceptionObject, "Uncaught error");
                    Environment.Exit(-1);
                };

                _logger.LogInformation("Wabbajack Build - {Sha}",ThisAssembly.Git.Sha);
                _logger.LogInformation("Running in {EntryPoint}", KnownFolders.EntryPoint);

                var p = _systemParams.Create();

                _logger.LogInformation("Detected Windows Version: {Version}", Environment.OSVersion.VersionString);
                
                _logger.LogInformation(
                    "System settings - ({MemorySize} RAM) ({PageSize} Page), Display: {ScreenWidth} x {ScreenHeight} ({Vram} VRAM - VideoMemorySizeMb={ENBVRam})",
                    p.SystemMemorySize.ToFileSizeString(), p.SystemPageSize.ToFileSizeString(), p.ScreenWidth, p.ScreenHeight, p.VideoMemorySize.ToFileSizeString(), p.EnbLEVRAMSize);

                if (p.SystemPageSize == 0)
                    _logger.LogInformation("Pagefile is disabled! Consider increasing to 20000MB. A disabled pagefile can cause crashes and poor in-game performance");
                else if (p.SystemPageSize < 2e+10)
                    _logger.LogInformation("Pagefile below recommended! Consider increasing to 20000MB. A suboptimal pagefile can cause crashes and poor in-game performance");

                //Warmup();
                
                var _ = updater.Run();

                var (settings, loadedSettings) = MainSettings.TryLoadTypicalSettings().AsTask().Result;
                // Load settings
                /*
                if (CLIArguments.NoSettings || !loadedSettings)
                {
                    _settings = new MainSettings {Version = Consts.SettingsVersion};
                    RunWhenLoaded(DefaultSettings);
                }
                else
                {
                    _settings = settings;
                    RunWhenLoaded(LoadSettings);
                }*/
                

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
                _logger.LogError(ex, "During Main Window Startup");
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
            Task.Run(AssociateListsWithWabbajack).FireAndForget();
        }

        /// <summary>
        /// Run logic to associate wabbajack lists with this app in the background
        /// </summary>
        private void AssociateListsWithWabbajack()
        {
            /* TODO
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
            }*/
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
