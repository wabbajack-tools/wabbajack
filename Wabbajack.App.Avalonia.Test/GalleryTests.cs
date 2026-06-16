using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using ReactiveUI;
using Wabbajack;
using Wabbajack.Messages;

namespace WabbajackAvalonia.Test;

[NotInParallel] // shares the process-global headless Avalonia platform
public class GalleryTests
{
    [Test]
    public async Task Tile_Renders()
    {
        var titles = await HeadlessSession.Dispatch(async () =>
        {
            var vm = TestVm.ModlistTile();

            var view = new ModListTileView { DataContext = vm, ViewModel = vm };
            var window = new Window { Content = view, Width = 1000, Height = 700 };
            window.Show();

            // Realize the visual tree via explicit measure/arrange (see HomeViewTests for why we avoid
            // flushing the deferred Loaded queue under the headless backend).
            window.Measure(new Size(1000, 700));
            window.Arrange(new Rect(0, 0, 1000, 700));

            var descendants = view.GetVisualDescendants().OfType<Control>().ToList();
            var hasImage = descendants.OfType<Image>().Any();
            var titleTexts = descendants.OfType<TextBlock>().Select(tb => tb.Text).ToList();

            // Close before returning: leaving multiple shown headless windows open across
            // [NotInParallel] tests deadlocks the shared single-threaded session.
            window.Close();

            return (hasImage, titleTexts);
        });

        await Assert.That(titles.hasImage).IsTrue();
        await Assert.That(titles.titleTexts.Contains(TestVm.TileTitle)).IsTrue();
    }

    [Test]
    public async Task Tile_DetailsCommand_FiresMessages()
    {
        var (floatingScreen, detailsVm) = await HeadlessSession.Dispatch(async () =>
        {
            var vm = TestVm.ModlistTile();

            FloatingScreenType? screen = null;
            BaseModListMetadataVM? loadedVm = null;
            using var floatingSub = MessageBus.Current.Listen<ShowFloatingWindow>()
                .Subscribe(m => screen = m.Screen);
            using var detailsSub = MessageBus.Current.Listen<LoadModlistForDetails>()
                .Subscribe(m => loadedVm = m.MetadataVM);

            // DetailsCommand is synchronous: sends LoadModlistForDetails + ShowFloatingWindow. No network.
            vm.DetailsCommand.Execute(null);

            return (screen, loadedVm);
        });

        await Assert.That(floatingScreen).IsEqualTo(FloatingScreenType.ModListDetails);
        await Assert.That(detailsVm).IsNotNull();
    }
}
