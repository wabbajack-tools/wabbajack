using System.Linq;
using System.Threading.Tasks;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Wabbajack;
using Wabbajack.Blazor.Components;
using Wabbajack.Messages;

namespace Wabbajack.Blazor.Test;

// Regression: the details overlay must expose the Install action (it was previously Close-only, so there
// was no way to start an install anywhere in the app).
[NotInParallel] // shares process-global MessageBus.Current / RxApp scheduler
public class DetailsPanelTests
{
    [Test]
    public async Task DetailsPanel_HasInstallAndActionButtons()
    {
        using var ctx = new BunitContext();
        TestSupport.Register(ctx.Services);

        // Resolve the (singleton) details VM first so it's subscribed, then populate it via the message.
        var detailsVm = ctx.Services.GetRequiredService<ModListDetailsVM>();
        var tile = TestSupport.CreateTile(ctx.Services);
        LoadModlistForDetails.Send(tile);

        var cut = ctx.Render<ModListDetailsPanel>(ps => ps.Add(p => p.Vm, detailsVm));

        var buttons = cut.FindAll("button").Select(b => b.TextContent.Trim()).ToList();
        await Assert.That(buttons.Any(t => t.Contains("Install"))).IsTrue();
        await Assert.That(buttons.Any(t => t.Contains("Website"))).IsTrue();
        await Assert.That(buttons.Any(t => t.Contains("Close"))).IsTrue();
    }
}
