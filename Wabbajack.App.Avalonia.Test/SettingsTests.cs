using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Wabbajack;

namespace WabbajackAvalonia.Test;

[NotInParallel] // shares the process-global headless Avalonia platform
public class SettingsTests
{
    [Test]
    public async Task Settings_Renders_SubViews()
    {
        var (hasPerformance, hasAbout) = await HeadlessSession.Dispatch(async () =>
        {
            var vm = TestVm.Settings();

            var view = new SettingsView { DataContext = vm, ViewModel = vm };
            var window = new Window { Content = view, Width = 1000, Height = 700 };
            window.Show();

            // Realize the visual tree via explicit measure/arrange (see HomeViewTests for why we avoid
            // flushing the deferred Loaded queue under the headless backend).
            window.Measure(new Size(1000, 700));
            window.Arrange(new Rect(0, 0, 1000, 700));

            var descendants = view.GetVisualDescendants().OfType<Control>().ToList();
            var perf = descendants.OfType<global::Wabbajack.PerformanceSettingsView>().Any();
            var about = descendants.OfType<global::Wabbajack.AboutView>().Any();

            // Close before returning: leaving multiple shown headless windows open across
            // [NotInParallel] tests deadlocks the shared single-threaded session.
            window.Close();

            return (perf, about);
        });

        await Assert.That(hasPerformance).IsTrue();
        await Assert.That(hasAbout).IsTrue();
    }

    [Test]
    public async Task Settings_SubVMs_AreConstructed()
    {
        var vm = await HeadlessSession.Dispatch(async () => TestVm.Settings());

        // Deterministic offline assertions: the SettingsVM shell constructed all of its sub-VMs.
        await Assert.That(vm.ResetCommand).IsNotNull();
        await Assert.That(vm.PerformanceVM).IsNotNull();
        await Assert.That(vm.AboutVM).IsNotNull();
        await Assert.That(vm.LoginVM).IsNotNull();
    }
}
