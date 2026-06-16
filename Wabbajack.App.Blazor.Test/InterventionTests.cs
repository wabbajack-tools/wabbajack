using System.Linq;
using System.Threading.Tasks;
using Bunit;
using Wabbajack.Blazor.Components;

namespace Wabbajack.Blazor.Test;

[NotInParallel] // shares process-global RxApp scheduler (ReactiveCommand delivery)
public class InterventionTests
{
    private sealed class FakeConfirm : global::Wabbajack.ConfirmationIntervention
    {
        public override string ShortDescription => "Test confirm?";
        public override string ExtendedDescription => "Are you sure about the test?";
    }

    [Test]
    public async Task InterventionModal_Confirm_CompletesContinue()
    {
        using var ctx = new BunitContext();
        var iv = new FakeConfirm();

        var cut = ctx.Render<InterventionModal>(ps => ps.Add(p => p.Intervention, iv));

        await Assert.That(cut.Markup).Contains("Test confirm?");
        await Assert.That(cut.Markup).Contains("Are you sure about the test?");

        cut.FindAll("button").First(b => b.TextContent.Contains("Continue")).Click();

        // ConfirmCommand resolves the intervention's task with Continue.
        var result = await iv.Task;
        await Assert.That(result).IsEqualTo(global::Wabbajack.ConfirmationIntervention.Choice.Continue);
        await Assert.That(iv.Handled).IsTrue();
    }
}
