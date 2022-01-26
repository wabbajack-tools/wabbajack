using System;
using Avalonia;
using Avalonia.ReactiveUI;
using CefNet;
using Wabbajack.Networking.Browser.ViewModels;
using Wabbajack.Networking.Browser.Views;

namespace Wabbajack.Networking.Browser
{
    class Program
    {
        public static MainWindowViewModel MainWindowVM { get; set; }
        public static string[] Args { get; set; }

        public static Views.MainWindow MainWindow { get; set; }

        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args)
        {
            Args = args;
            BuildAvaloniaApp()
                .StartWithCefNetApplicationLifetime(args);
            
            
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .LogToTrace()
                .UseReactiveUI();
    }
}
