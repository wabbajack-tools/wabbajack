using System.Threading.Tasks;
using Avalonia;
using Avalonia.Styling;
using Wabbajack.Messages;
using Wabbajack.Views;

namespace WabbajackAvalonia.Test;

// Shell-level interaction tests: they drive the real MainWindow (built against the offline test DI
// container) the way the running app does, so we catch wiring bugs that view-in-isolation tests miss.
[NotInParallel] // shares the process-global headless Avalonia platform
public class ShellInteractionTests
{
    // Regression for the gallery tile crash: clicking a tile fires LoadModlistForDetails *then*
    // ShowFloatingWindow. The details VM must already be listening when the first message arrives,
    // otherwise MetadataVM is null and the view NREs on activation. Here we invoke the tile's real
    // DetailsCommand against a real MainWindow and assert the floating details VM received the tile.
    [Test]
    public async Task ClickingGalleryTile_PopulatesDetailsViewModel()
    {
        var (metadataMatches, hostsDetailsVm) = await HeadlessSession.Dispatch(async () =>
        {
            // Construct (don't Show) the real shell: showing it would render the embedded Gabarito font,
            // which the headless drawing backend can't shape. Construction alone wires up the persistent
            // floating VMs exactly as the running app does.
            var window = new MainWindow(TestVm.Services);
            try
            {
                var tile = TestVm.ModlistTile();

                // Exactly what a real tile click does: BaseModListMetadataVM.DetailsCommand sends
                // LoadModlistForDetails(this) and then ShowFloatingWindow(ModListDetails).
                tile.DetailsCommand.Execute(null);

                // The overlay would host this VM. It must already carry the clicked tile's metadata —
                // pre-fix it was a freshly-resolved VM that missed the message and NRE'd on activation.
                var hosted = window.FloatingContentFor(FloatingScreenType.ModListDetails);
                var detailsVm = hosted as global::Wabbajack.ModListDetailsVM;
                var matches = detailsVm != null && ReferenceEquals(detailsVm.MetadataVM, tile);

                return (matches, detailsVm != null);
            }
            finally
            {
                window.DisposeMessageSubscriptions();
            }
        });

        await Assert.That(hostsDetailsVm).IsTrue();
        await Assert.That(metadataMatches).IsTrue();
    }

    // Guards the dark-theme fix: the app forces RequestedThemeVariant="Dark" so Fluent's controls use
    // their light-on-dark palette. Without it the variant follows the OS and text renders black on the
    // dark Wabbajack background.
    [Test]
    public async Task App_UsesDarkThemeVariant()
    {
        var variant = await HeadlessSession.Dispatch(async () =>
            Application.Current!.ActualThemeVariant);

        await Assert.That(variant).IsEqualTo(ThemeVariant.Dark);
    }
}
