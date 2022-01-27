using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using CefNet;
using Wabbajack.Paths.IO;

namespace Wabbajack.CLI.Browser
{
    public partial class BrowserApp : Application
    {
        private const int messagePumpDelay = 10;


        private static CefAppImpl app;
        private static Timer messagePump;
        
        private CefAppImpl _cefAppImpl;
        public TaskCompletionSource OnComplete { get; init; }
        public IServiceProvider Provider { get; init; }
        public static Window? MainWindow { get; set; }

        public static event EventHandler FrameworkInitialized;
        public static event EventHandler FrameworkShutdown;

        public override void Initialize()
        {
            var resources = KnownFolders.EntryPoint;
            var settings = new CefSettings
            {
                NoSandbox = true,
                PersistSessionCookies = true,
                MultiThreadedMessageLoop = false,
                WindowlessRenderingEnabled = true,
                ExternalMessagePump = true,
                LocalesDirPath = resources.Combine("locales").ToString(),
                ResourcesDirPath = resources.ToString(),
                UserAgent = "",
                CachePath = KnownFolders.WabbajackAppLocal.Combine("cef_cache").ToString()
            };
            
            var app = new CefAppImpl();
            app.ScheduleMessagePumpWorkCallback = OnScheduleMessagePumpWork;

            app.CefProcessMessageReceived += App_CefProcessMessageReceived;
            app.Initialize(resources.ToString(), settings);

            Dispatcher.UIThread.Post(() => Thread.CurrentThread.Name = "UIThread");
            AvaloniaXamlLoader.Load(this);
        }
        
        private static async void OnScheduleMessagePumpWork(long delayMs)
        {
            await Task.Delay((int) delayMs);
            Dispatcher.UIThread.Post(CefApi.DoMessageLoopWork);
        }

        private static void App_CefProcessMessageReceived(object? sender, CefProcessMessageReceivedEventArgs e)
        {
            var msg = e.Name;
        }

        public override void OnFrameworkInitializationCompleted()
        {
            base.OnFrameworkInitializationCompleted();
            if (CefNetApplication.Instance.UsesExternalMessageLoop)
                messagePump = new Timer(_ => Dispatcher.UIThread.Post(CefApi.DoMessageLoopWork), null, messagePumpDelay,
                    messagePumpDelay);
            OnComplete.SetResult();

        }

        private void Startup(object sender, ControlledApplicationLifetimeStartupEventArgs e)
        {
            FrameworkInitialized?.Invoke(this, EventArgs.Empty);
        }

        private void Exit(object sender, ControlledApplicationLifetimeExitEventArgs e)
        {
            FrameworkShutdown?.Invoke(this, EventArgs.Empty);
        }
    }
}