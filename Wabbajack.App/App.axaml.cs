using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using Splat;
using Wabbajack.App.Converters;
using Wabbajack.App.Utilities;
using Wabbajack.App.Views;

namespace Wabbajack.App;

public class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    public static Window? MainWindow { get; set; }

    public static event EventHandler FrameworkInitialized;
    public static event EventHandler FrameworkShutdown;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var host = Host.CreateDefaultBuilder(Array.Empty<string>())
            .ConfigureLogging(c => { c.ClearProviders(); })
            .ConfigureServices((host, services) => { services.AddAppServices(); }).Build();
        Services = host.Services;

        SetupConverters();

        // Need to startup the message bus;
        Services.GetService<MessageBus>();
        var app = Services.GetService<CefAppImpl>();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
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

    private void SetupConverters()
    {
        Locator.CurrentMutable.RegisterConstant<IBindingTypeConverter>(new AbsoultePathBindingConverter());
    }
}