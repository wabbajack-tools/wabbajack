using Avalonia;
using Avalonia.Headless;
using Avalonia.ReactiveUI;

namespace WabbajackAvalonia.Test;

// Entry point for the headless Avalonia platform. HeadlessUnitTestSession.StartNew(typeof(TestApp))
// discovers this static BuildAvaloniaApp() method and uses it to construct the AppBuilder.
public static class TestApp
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<global::Wabbajack.App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true })
            .UseReactiveUI();
}
