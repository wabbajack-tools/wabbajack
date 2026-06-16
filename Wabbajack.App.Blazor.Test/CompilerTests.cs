using System.Linq;
using System.Threading.Tasks;
using Bunit;
using Wabbajack.Blazor.Components;
using Wabbajack.Messages;

namespace Wabbajack.Blazor.Test;

// The compiler screens default to a Configuration state and don't start compiling or hit the network in
// their constructors / on activation, so we assert on the static default markup and command wiring.
[NotInParallel] // shares process-global MessageBus.Current / RxApp scheduler
public class CompilerTests
{
    [Test]
    public async Task CompilerHome_RendersActions_AndRecentSection()
    {
        using var ctx = new BunitContext();
        TestSupport.Register(ctx.Services);

        var cut = ctx.Render<CompilerHomePage>();

        // The two "big button" actions ported from WPF CompilerHomeView.
        await Assert.That(cut.Markup).Contains("Compile a new list");
        await Assert.That(cut.Markup).Contains("Load compiler settings");
        await Assert.That(cut.Markup).Contains("Recently Compiled Modlists");
    }

    [Test]
    public async Task CompilerMain_RendersDetailsForm_AndCompileButton()
    {
        using var ctx = new BunitContext();
        TestSupport.Register(ctx.Services);

        var cut = ctx.Render<CompilerMainPage>();

        // Detail form labels (CompilerDetailsVM.Settings) and the source-files pane render.
        await Assert.That(cut.Markup).Contains("Author");
        await Assert.That(cut.Markup).Contains("Version");
        await Assert.That(cut.Markup).Contains("Machine URL");
        await Assert.That(cut.Markup).Contains("Source Files");

        // The compile action button is present (disabled by default since required fields are empty).
        await Assert.That(cut.FindAll("button").Any(b => b.TextContent.Contains("Compile List"))).IsTrue();

        // Defaults to the Configuration state on activation; nothing started compiling.
        await Assert.That(cut.Markup).Contains("Configuration");
    }

    [Test]
    public async Task CompilerMain_StartButton_DisabledByDefault()
    {
        using var ctx = new BunitContext();
        TestSupport.Register(ctx.Services);

        var cut = ctx.Render<CompilerMainPage>();

        // With empty settings the StartCommand CanExecute is false, so the button is disabled.
        var compileBtn = cut.FindAll("button").First(b => b.TextContent.Contains("Compile List"));
        await Assert.That(compileBtn.HasAttribute("disabled")).IsTrue();
    }
}
