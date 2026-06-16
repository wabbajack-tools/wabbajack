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
public class FloatingWindowTests
{
    [Test]
    public async Task InfoView_Renders()
    {
        const string infoText = "hello from the info screen";

        var (hasButton, infoTexts) = await HeadlessSession.Dispatch(async () =>
        {
            var vm = TestVm.Info();
            // Set Info via the message the VM subscribes to (it also sets CloseCommand).
            LoadInfoScreen.Send(infoText, null);

            var view = new global::Wabbajack.InfoView { DataContext = vm, ViewModel = vm };
            var window = new Window { Content = view, Width = 1000, Height = 700 };
            window.Show();

            // Realize the visual tree via explicit measure/arrange (see HomeViewTests for why we avoid
            // flushing the deferred Loaded queue under the headless backend).
            window.Measure(new Size(1000, 700));
            window.Arrange(new Rect(0, 0, 1000, 700));

            var descendants = view.GetVisualDescendants().OfType<Control>().ToList();
            var button = descendants.OfType<global::Wabbajack.WJButton>().Any();
            var texts = descendants.OfType<TextBlock>().Select(tb => tb.Text).ToList();

            // Close before returning: leaving multiple shown headless windows open across
            // [NotInParallel] tests deadlocks the shared single-threaded session.
            window.Close();

            return (button, texts);
        });

        // The view realized its tree: the Back WJButton rendered and the Info TextBlock carries our text.
        await Assert.That(hasButton).IsTrue();
        await Assert.That(infoTexts.Contains(infoText)).IsTrue();
    }

    [Test]
    public async Task FileUploadView_Renders()
    {
        var (hasPicker, hasButton) = await HeadlessSession.Dispatch(async () =>
        {
            var vm = TestVm.FileUpload();

            var view = new global::Wabbajack.FileUploadView { DataContext = vm, ViewModel = vm };
            var window = new Window { Content = view, Width = 1000, Height = 700 };
            window.Show();

            window.Measure(new Size(1000, 700));
            window.Arrange(new Rect(0, 0, 1000, 700));

            var descendants = view.GetVisualDescendants().OfType<Control>().ToList();
            var picker = descendants.OfType<global::Wabbajack.FilePicker>().Any();
            var button = descendants.OfType<global::Wabbajack.WJButton>().Any();

            window.Close();

            return (picker, button);
        });

        await Assert.That(hasPicker || hasButton).IsTrue();
    }

    [Test]
    public async Task ModListDetailsView_Renders()
    {
        var (hasButton, hasText) = await HeadlessSession.Dispatch(async () =>
        {
            // Factory populates MetadataVM via LoadModlistForDetails so the view's MetadataVM.* bindings resolve.
            var vm = TestVm.ModListDetails();

            var view = new global::Wabbajack.ModListDetailsView { DataContext = vm, ViewModel = vm };
            var window = new Window { Content = view, Width = 1000, Height = 700 };
            window.Show();

            window.Measure(new Size(1000, 700));
            window.Arrange(new Rect(0, 0, 1000, 700));

            var descendants = view.GetVisualDescendants().OfType<Control>().ToList();
            var button = descendants.OfType<global::Wabbajack.WJButton>().Any();
            var text = descendants.OfType<TextBlock>().Any();

            window.Close();

            return (button, text);
        });

        await Assert.That(hasButton || hasText).IsTrue();
    }

    [Test]
    public async Task Close_FiresFloatingNone()
    {
        var screen = await HeadlessSession.Dispatch(async () =>
        {
            var vm = TestVm.FileUpload();

            FloatingScreenType? fired = null;
            using var sub = MessageBus.Current.Listen<ShowFloatingWindow>()
                .Subscribe(m => fired = m.Screen);

            // FileUploadVM.CloseCommand sends ShowFloatingWindow(FloatingScreenType.None).
            vm.CloseCommand.Execute(null);

            return fired;
        });

        await Assert.That(screen).IsEqualTo(FloatingScreenType.None);
    }
}
