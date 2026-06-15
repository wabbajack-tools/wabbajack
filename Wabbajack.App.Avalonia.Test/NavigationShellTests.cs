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
public class NavigationShellTests
{
    [Test]
    public async Task NavigationView_RendersButtons()
    {
        var buttons = await HeadlessSession.Dispatch(async () =>
        {
            var vm = TestVm.Navigation();

            var view = new global::Wabbajack.NavigationView { DataContext = vm, ViewModel = vm };
            var window = new Window { Content = view, Width = 1000, Height = 700 };
            window.Show();

            // Realize the visual tree via an explicit measure/arrange pass (see HomeViewTests for why
            // we avoid RunJobs: FluentIcons glyph shaping runs from the deferred Loaded queue and crashes
            // under the headless drawing backend).
            window.Measure(new Size(1000, 700));
            window.Arrange(new Rect(0, 0, 1000, 700));

            var found = view.GetVisualDescendants().OfType<Button>().ToList();

            // Close the headless window so its dispatcher resources are released before the next
            // [NotInParallel] test runs — leaving multiple shown headless windows open across tests
            // can deadlock the shared single-threaded headless session at teardown.
            window.Close();

            return found;
        });

        // The sidebar realized its four nav buttons (Home/Browse/Compile/Settings).
        await Assert.That(buttons.Count).IsGreaterThanOrEqualTo(4);
    }

    [Test]
    public async Task NavigationCommands_FireGlobalNavigation()
    {
        var (browse, compile, settings) = await HeadlessSession.Dispatch(async () =>
        {
            var vm = TestVm.Navigation();

            ScreenType? last = null;
            using var sub = MessageBus.Current.Listen<NavigateToGlobal>().Subscribe(m => last = m.Screen);

            vm.BrowseCommand.Execute(null);
            var browseScreen = last;

            vm.CompileModListCommand.Execute(null);
            var compileScreen = last;

            vm.SettingsCommand.Execute(null);
            var settingsScreen = last;

            return (browseScreen, compileScreen, settingsScreen);
        });

        await Assert.That(browse).IsEqualTo(ScreenType.ModListGallery);
        await Assert.That(compile).IsEqualTo(ScreenType.CompilerHome);
        await Assert.That(settings).IsEqualTo(ScreenType.Settings);
    }
}
