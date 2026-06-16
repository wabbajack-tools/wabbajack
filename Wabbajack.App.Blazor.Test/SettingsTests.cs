using System.Linq;
using System.Threading.Tasks;
using Bunit;
using Wabbajack.Blazor.Components;

namespace Wabbajack.Blazor.Test;

[NotInParallel] // shares process-global MessageBus.Current / RxApp scheduler
public class SettingsTests
{
    [Test]
    public async Task Settings_RendersSections_AndLoginRow()
    {
        using var ctx = new BunitContext();
        TestSupport.Register(ctx.Services);

        var cut = ctx.Render<SettingsPage>();

        // The three panes ported from WPF SettingsView render.
        await Assert.That(cut.Markup).Contains("Accounts");
        await Assert.That(cut.Markup).Contains("Performance");
        await Assert.That(cut.Markup).Contains("About");

        // The registered Nexus login manager shows up as an account row with a login button.
        await Assert.That(cut.FindAll("button").Any(b => b.TextContent.Contains("Log in") || b.TextContent.Contains("Log out"))).IsTrue();
    }
}
