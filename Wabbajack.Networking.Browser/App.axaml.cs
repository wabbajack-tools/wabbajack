using System;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using Splat;
using Wabbajack.Networking.Browser.ViewModels;
using Wabbajack.Networking.Browser.Views;

namespace Wabbajack.Networking.Browser
{
    public class App : Application
    {
        public static IServiceProvider Services { get; private set; } = null!;
        public static Window? MainWindow { get; set; }

        public static event EventHandler FrameworkInitialized;
        public static event EventHandler FrameworkShutdown;

        public override void Initialize()
        {
            Dispatcher.UIThread.Post(() => Thread.CurrentThread.Name = "UIThread");
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            var host = Host.CreateDefaultBuilder(Array.Empty<string>())
                .ConfigureLogging(c => { c.ClearProviders(); })
                .ConfigureServices((host, services) => { services.AddAppServices(); }).Build();
            Services = host.Services;


            // Need to startup the message bus;
            var app = Services.GetService<CefAppImpl>();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                Program.MainWindow = Services.GetRequiredService<MainWindow>();
                Program.MainWindowVM = Services.GetRequiredService<MainWindowViewModel>();
                Program.MainWindow.ViewModel = Program.MainWindowVM;
                desktop.MainWindow = Program.MainWindow;
                desktop.Startup += Startup;
                desktop.Exit += Exit;
                MainWindow = desktop.MainWindow;
            }

            base.OnFrameworkInitializationCompleted();
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