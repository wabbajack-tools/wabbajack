using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Wabbajack;

namespace WabbajackAvalonia.Test;

[NotInParallel] // shares the process-global headless Avalonia platform
public class CompilerTests
{
    [Test]
    public async Task CompilerHome_Renders()
    {
        var hasBigButton = await HeadlessSession.Dispatch(async () =>
        {
            var vm = TestVm.CompilerHome();

            var view = new global::Wabbajack.CompilerHomeView { DataContext = vm, ViewModel = vm };
            var window = new Window { Content = view, Width = 1000, Height = 700 };
            window.Show();

            // Realize the visual tree via explicit measure/arrange (see HomeViewTests for why we avoid
            // flushing the deferred Loaded queue under the headless backend).
            window.Measure(new Size(1000, 700));
            window.Arrange(new Rect(0, 0, 1000, 700));

            var bigButton = view.GetVisualDescendants().OfType<Control>()
                .OfType<global::Wabbajack.BigButton>().Any();

            // Close before returning: leaving multiple shown headless windows open across
            // [NotInParallel] tests deadlocks the shared single-threaded session.
            window.Close();

            return bigButton;
        });

        // The compiler home screen renders its New/Load BigButtons.
        await Assert.That(hasBigButton).IsTrue();
    }

    [Test]
    public async Task CompilerMain_Renders_ConfigState()
    {
        var (hasConfigSection, vm) = await HeadlessSession.Dispatch(async () =>
        {
            var compilerMainVm = TestVm.CompilerMain();

            var view = new global::Wabbajack.CompilerMainView { DataContext = compilerMainVm, ViewModel = compilerMainVm };
            var window = new Window { Content = view, Width = 1000, Height = 700 };
            window.Show();

            window.Measure(new Size(1000, 700));
            window.Arrange(new Rect(0, 0, 1000, 700));

            var descendants = view.GetVisualDescendants().OfType<Control>().ToList();
            var configSection = descendants.OfType<global::Wabbajack.CompilerDetailsView>().Any()
                || descendants.OfType<global::Wabbajack.CompilerFileManagerView>().Any()
                || descendants.OfType<global::Wabbajack.WJButton>().Any();

            window.Close();

            return (configSection, compilerMainVm);
        });

        // In the default Configuration state the main screen renders its config UI (the details/file
        // manager sub-views and/or a WJButton). Either presence proves the config section was realized.
        await Assert.That(hasConfigSection).IsTrue();

        // Deterministic offline assertions: the VM ctor leaves the compiler in Configuration and wires
        // up the Start command. We never execute StartCommand (that would start a compile).
        await Assert.That(vm.State).IsEqualTo(global::Wabbajack.CompilerState.Configuration);
        await Assert.That(vm.StartCommand).IsNotNull();
    }
}
