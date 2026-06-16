using System.Linq;
using System.Threading.Tasks;
using Bunit;
using Wabbajack.Blazor.Components;

namespace Wabbajack.Blazor.Test;

[NotInParallel] // shares process-global MessageBus.Current / RxApp scheduler
public class InstallerTests
{
    [Test]
    public async Task Installer_RendersConfigurationState_WithPickersAndInstallButton()
    {
        using var ctx = new BunitContext();
        TestSupport.Register(ctx.Services);

        var cut = ctx.Render<InstallerPage>();

        // The VM's default InstallState is Configuration, so the configuration sub-layout renders.
        await Assert.That(cut.Markup).Contains("data-state=\"configuration\"");
        await Assert.That(cut.Markup).Contains("Configure installation");

        // The three file pickers ported from MO2InstallerConfigView (modlist / install / downloads).
        await Assert.That(cut.Markup).Contains("Modlist file");
        await Assert.That(cut.Markup).Contains("Install location");
        await Assert.That(cut.Markup).Contains("Downloads location");

        // The primary action from the WPF install button is present.
        await Assert.That(cut.FindAll("button").Any(b => b.TextContent.Contains("Begin Install"))).IsTrue();
    }
}
