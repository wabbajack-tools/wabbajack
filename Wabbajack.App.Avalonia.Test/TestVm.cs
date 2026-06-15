using System;
using Microsoft.Extensions.DependencyInjection;
using Wabbajack.DTOs.Interventions;
using Wabbajack.Services.OSIntegrated;

namespace WabbajackAvalonia.Test;

// Minimal offline DI container that builds Core ViewModels exactly like the WJ test harness:
// AddOSIntegrated with the local cache + stubbed game folders, and a throwing intervention handler.
public static class TestVm
{
    private static readonly IServiceProvider Sp = Build();

    private static IServiceProvider Build()
    {
        var s = new ServiceCollection();
        s.AddLogging();
        s.AddSingleton<IUserInterventionHandler, ThrowingUserInterventionHandler>();
        s.AddOSIntegrated(o =>
        {
            o.UseLocalCache = true;
            o.UseStubbedGameFolders = true;
        });
        s.AddTransient<global::Wabbajack.HomeVM>();
        s.AddTransient<global::Wabbajack.NavigationVM>();
        return s.BuildServiceProvider();
    }

    public static global::Wabbajack.HomeVM Home() => Sp.GetRequiredService<global::Wabbajack.HomeVM>();

    public static global::Wabbajack.NavigationVM Navigation() => Sp.GetRequiredService<global::Wabbajack.NavigationVM>();
}
