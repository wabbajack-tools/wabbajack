using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CefNet;
using Microsoft.Extensions.DependencyInjection;
using Wabbajack.CLI.TypeConverters;
using Wabbajack.CLI.Verbs;
using Wabbajack.Networking.Browser.Verbs;
using Wabbajack.Networking.Browser.ViewModels;
using Wabbajack.Networking.Browser.Views;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.Services.OSIntegrated;

namespace Wabbajack.Networking.Browser;

public static class ServiceExtensions
{
        private const int messagePumpDelay = 10;


    private static CefAppImpl app;
    private static Timer messagePump;

    public static IServiceCollection AddAppServices(this IServiceCollection services)
    {
        
        TypeDescriptor.AddAttributes(typeof(AbsolutePath),
            new TypeConverterAttribute(typeof(AbsolutePathTypeConverter)));
        
        var resources = KnownFolders.EntryPoint;
        services.AddSingleton<MainWindow>();
        services.AddSingleton<MainWindowViewModel>();

        services.AddSingleton<IVerb, NexusLogin>();
        services.AddSingleton<IVerb, LoverLabLogin>();
        services.AddSingleton<IVerb, VectorPlexusLogin>();
        services.AddSingleton<IVerb, ManualDownload>();
        services.AddOSIntegrated();
        
        services.AddSingleton(s => new CefSettings
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
        });

        services.AddSingleton(s =>
        {
            App.FrameworkInitialized += App_FrameworkInitialized;

            var app = new CefAppImpl();
            app.ScheduleMessagePumpWorkCallback = OnScheduleMessagePumpWork;

            app.CefProcessMessageReceived += App_CefProcessMessageReceived;
            app.Initialize(resources.ToString(), s.GetService<CefSettings>());


            return app;
        });

        return services;
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

    private static void App_FrameworkInitialized(object? sender, EventArgs e)
    {
        if (CefNetApplication.Instance.UsesExternalMessageLoop)
            messagePump = new Timer(_ => Dispatcher.UIThread.Post(CefApi.DoMessageLoopWork), null, messagePumpDelay,
                messagePumpDelay);
    }
}