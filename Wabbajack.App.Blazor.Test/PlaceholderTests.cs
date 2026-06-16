using System.Threading.Tasks;
using Bunit;
using Wabbajack.Blazor.Components;
using Wabbajack.Messages;

namespace Wabbajack.Blazor.Test;

// Smoke test to pin down the bUnit v2 API (BunitContext + Render<T>) before building the rest.
[NotInParallel] // shares process-global MessageBus.Current / RxApp scheduler
public class PlaceholderTests
{
    [Test]
    public async Task Placeholder_ShowsScreenName()
    {
        using var ctx = new BunitContext();
        var cut = ctx.Render<ScreenPlaceholder>(ps => ps.Add(p => p.Screen, ScreenType.Settings));
        await Assert.That(cut.Markup).Contains("Settings");
    }
}
