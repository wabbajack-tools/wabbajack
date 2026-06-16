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

    // EVIDENCE: is the InstallCommand gated (CanExecute false), or does executing it actually run?
    [Test]
    public async Task InstallCommand_CanExecute_IsTrue()
    {
        using var ctx = new BunitContext();
        TestSupport.Register(ctx.Services);
        var tile = TestSupport.CreateTile(ctx.Services);

        await Assert.That(tile.InstallCommand.CanExecute(null)).IsTrue();
    }

    // EVIDENCE: executing the command enters the download path (Status -> Downloading). If this holds,
    // the click DOES work — it just downloads silently with no UI feedback, which reads as "nothing".
    [Test]
    public async Task InstallCommand_Execute_EntersDownloadingState()
    {
        using var ctx = new BunitContext();
        TestSupport.Register(ctx.Services);
        var tile = TestSupport.CreateTile(ctx.Services);

        tile.InstallCommand.Execute(null);

        // Poll briefly: HaveModList is false for the fake tile, so Download() runs and sets Status.
        for (var i = 0; i < 50 && tile.Status != BaseModListMetadataVM.ModListStatus.Downloading; i++)
            await Task.Delay(100);

        await Assert.That(tile.Status).IsEqualTo(BaseModListMetadataVM.ModListStatus.Downloading);
    }
}
