using System;
using System.Threading.Tasks;
using Bunit;
using ReactiveUI;
using Wabbajack;
using Wabbajack.Blazor.Components;
using Wabbajack.Messages;

namespace Wabbajack.Blazor.Test;

// Behavior parity with the old Avalonia gallery-tile tests: a tile renders its title, and clicking it
// fires LoadModlistForDetails + ShowFloatingWindow(ModListDetails) so the shell opens the overlay.
[NotInParallel] // shares process-global MessageBus.Current / RxApp scheduler
public class GalleryTileTests
{
    [Test]
    public async Task Tile_RendersTitle()
    {
        using var ctx = new BunitContext();
        TestSupport.Register(ctx.Services);
        var tile = TestSupport.CreateTile(ctx.Services);

        var cut = ctx.Render<ModListTile>(ps => ps.Add(p => p.Tile, tile));

        await Assert.That(cut.Markup).Contains(TestSupport.TileTitle);
    }

    [Test]
    public async Task Tile_Click_FiresDetailMessages()
    {
        using var ctx = new BunitContext();
        TestSupport.Register(ctx.Services);
        var tile = TestSupport.CreateTile(ctx.Services);

        BaseModListMetadataVM? loaded = null;
        FloatingScreenType? floating = null;
        using var s1 = MessageBus.Current.Listen<LoadModlistForDetails>().Subscribe(m => loaded = m.MetadataVM);
        using var s2 = MessageBus.Current.Listen<ShowFloatingWindow>().Subscribe(m => floating = m.Screen);

        var cut = ctx.Render<ModListTile>(ps => ps.Add(p => p.Tile, tile));
        cut.Find(".tile").Click();

        await Assert.That(loaded).IsEqualTo(tile);
        await Assert.That(floating).IsEqualTo(FloatingScreenType.ModListDetails);
    }
}
