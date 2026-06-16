using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Wabbajack;

namespace WabbajackAvalonia.Test;

[NotInParallel] // shares the process-global headless Avalonia platform
public class InstallerTests
{
    [Test]
    public async Task Installer_Renders_ConfigState()
    {
        var (hasConfigView, hasButton) = await HeadlessSession.Dispatch(async () =>
        {
            var vm = TestVm.Installer();

            var view = new global::Wabbajack.InstallationView { DataContext = vm, ViewModel = vm };
            var window = new Window { Content = view, Width = 1000, Height = 700 };
            window.Show();

            // Realize the visual tree via explicit measure/arrange (see HomeViewTests for why we avoid
            // flushing the deferred Loaded queue under the headless backend).
            window.Measure(new Size(1000, 700));
            window.Arrange(new Rect(0, 0, 1000, 700));

            var descendants = view.GetVisualDescendants().OfType<Control>().ToList();
            var configView = descendants.OfType<global::Wabbajack.MO2InstallerConfigView>().Any();
            var button = descendants.OfType<global::Wabbajack.WJButton>().Any();

            // Close before returning: leaving multiple shown headless windows open across
            // [NotInParallel] tests deadlocks the shared single-threaded session.
            window.Close();

            return (configView, button);
        });

        // In the default Configuration state the installer renders its config UI (the MO2 config view
        // and/or the Install button). Either presence proves the Configuration section was realized.
        await Assert.That(hasConfigView || hasButton).IsTrue();
    }

    [Test]
    public async Task Installer_DefaultState_IsConfiguration()
    {
        var vm = await HeadlessSession.Dispatch(async () => TestVm.Installer());

        // Deterministic offline assertions: the VM ctor leaves the installer in Configuration and wires
        // up the Install command. We never execute InstallCommand (that would start an install).
        await Assert.That(vm.InstallState).IsEqualTo(global::Wabbajack.InstallState.Configuration);
        await Assert.That(vm.InstallCommand).IsNotNull();
    }
}
