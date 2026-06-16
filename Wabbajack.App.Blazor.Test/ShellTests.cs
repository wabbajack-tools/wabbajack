using System.Linq;
using System.Threading.Tasks;
using Bunit;
using Wabbajack.Blazor.Components;

namespace Wabbajack.Blazor.Test;

// The shell composes the nav sidebar and defaults to Home (ActiveScreen defaults to ScreenType.Home,
// so the Home nav button is the active one).
[NotInParallel] // shares process-global MessageBus.Current / RxApp scheduler
public class ShellTests
{
    [Test]
    public async Task Shell_RendersNav_AndDefaultsToHome()
    {
        using var ctx = new BunitContext();
        TestSupport.Register(ctx.Services);

        var cut = ctx.Render<Shell>();

        await Assert.That(cut.Markup).Contains("Browse lists");

        var active = cut.FindAll("button.nav-btn.active");
        await Assert.That(active.Count).IsEqualTo(1);
        await Assert.That(active.First().TextContent).Contains("Home");
    }
}
