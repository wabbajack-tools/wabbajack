using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.ReactiveUI;
using FluentAssertions;
using Wabbajack;

[assembly: AvaloniaTestApplication(typeof(Wabbajack.AvaloniaTests.HeadlessAppBuilder))]

namespace Wabbajack.AvaloniaTests;

public static class HeadlessAppBuilder
{
    // Configuring the real App type is the point: it loads App.axaml, which merges Themes/Colors,
    // Converters, Assets and CustomControls and applies Base/Controls. A view that references a
    // resource key or writes a selector those files don't support only fails once that markup is
    // live, so a bare AppBuilder would test nothing.
    //
    // UseReactiveUI mirrors Program.BuildAvaloniaApp: it registers the activation-for-view-fetcher
    // that every ReactiveUserControl here needs, and without it the views throw for a reason the
    // real app never hits.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
                  .UseHeadless(new AvaloniaHeadlessPlatformOptions())
                  .UseReactiveUI();
}

/// <summary>
/// Constructs every view in the app once, under a headless Avalonia application.
///
/// The WPF -> Avalonia port kept hitting the same failure: a view's XAML referenced a style key
/// that no longer existed (MainTextBoxStyle, WJButtonStyle, CircleButtonStyle...), or used markup
/// Avalonia rejects at runtime rather than at build time (a ControlTheme containing a descendant
/// selector). Every one of those compiles cleanly and throws only when the view is first built -
/// at which point Avalonia silently keeps the previously-displayed content, so the app looks like
/// it simply refuses to switch panels. Several went unnoticed because the affected screens
/// (the compiler settings page, the installation page) need a real modlist to reach by hand.
///
/// Instantiating each view is enough to surface all of it: resource lookups and style application
/// happen during construction.
/// </summary>
public class ViewConstructionTests
{
    [AvaloniaFact]
    public void EveryViewConstructsWithoutThrowing()
    {
        var types = ViewTypes().ToList();

        // Without this the test passes vacuously if the filter below ever stops matching - which is
        // exactly the failure mode a reflection-driven sweep hides. The app has ~45 views.
        types.Should().HaveCountGreaterThan(30, "the sweep must actually be finding the app's views");

        var failures = new List<string>();

        foreach (var type in types)
        {
            try
            {
                Activator.CreateInstance(type);
            }
            catch (Exception ex)
            {
                // The TargetInvocationException wrapper tells us nothing; the inner cause carries the
                // message identifying the missing key or the rejected selector.
                var cause = ex is TargetInvocationException { InnerException: { } inner } ? inner : ex;
                failures.Add($"{type.FullName}: {cause.GetType().Name}: {cause.Message}");
            }
        }

        // Joined rather than asserted as a collection so a failure reports every broken view at once;
        // the collection form truncates to the first item, which turns fixing these into one
        // rebuild per view.
        string.Join(Environment.NewLine, failures)
              .Should().BeEmpty("every view must build under the application's themes and resources");
    }

    private static IEnumerable<Type> ViewTypes() =>
        typeof(App).Assembly
            .GetTypes()
            .Where(t => t is { IsAbstract: false, IsGenericTypeDefinition: false }
                        && typeof(Control).IsAssignableFrom(t)
                        // Windows are owned by the DI container and pull in a real service graph.
                        && !typeof(Window).IsAssignableFrom(t)
                        && t.GetConstructor(Type.EmptyTypes) != null)
            .OrderBy(t => t.FullName, StringComparer.Ordinal);
}
