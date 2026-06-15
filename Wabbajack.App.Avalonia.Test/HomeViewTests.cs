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
public class HomeViewTests
{
    [Test]
    public async Task HomeView_RendersAndBrowseCommandNavigates()
    {
        var (rendered, navigatedTo) = await HeadlessSession.Dispatch(async () =>
        {
            var vm = TestVm.Home();

            var view = new HomeView { DataContext = vm, ViewModel = vm };
            var window = new Window { Content = view, Width = 1000, Height = 700 };
            window.Show();

            // Realize the visual tree via an explicit measure/arrange pass. This applies the window's
            // content template (so HomeView's children are built) without flushing the dispatcher's
            // deferred Loaded queue — under the headless drawing backend FluentIcons' glyph font cannot
            // be shaped, and that shaping only runs from the queued Loaded handlers, not from layout.
            window.Measure(new Size(1000, 700));
            window.Arrange(new Rect(0, 0, 1000, 700));

            // The view is "rendered" when its content has produced a realized visual tree.
            var descendants = view.GetVisualDescendants().OfType<Control>().ToList();

            ScreenType? screen = null;
            using var sub = MessageBus.Current.Listen<NavigateToGlobal>().Subscribe(m => screen = m.Screen);

            vm.BrowseCommand.Execute(null);

            return (descendants, screen);
        });

        // Core proof: the view realized a full visual tree (BigButton + LinksView both rendered)
        // AND BrowseCommand fired the global navigation.
        await Assert.That(rendered.OfType<global::Wabbajack.BigButton>().Any()).IsTrue();
        await Assert.That(rendered.OfType<global::Wabbajack.LinksView>().Any()).IsTrue();
        await Assert.That(navigatedTo).IsEqualTo(ScreenType.ModListGallery);
    }
}
