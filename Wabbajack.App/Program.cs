using System;
using Avalonia;
using Avalonia.ReactiveUI;
using CefNet;

namespace Wabbajack.App
{

    class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args) => BuildAvaloniaApp()
            .StartWithCefNetApplicationLifetime(args);

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .AfterSetup(AfterSetupCallback)
                .LogToTrace()
                .UseReactiveUI();

        private static void AfterSetupCallback(AppBuilder obj)
        {
        }
    }
}
