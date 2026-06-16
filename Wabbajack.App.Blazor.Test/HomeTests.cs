using System.Threading.Tasks;
using Bunit;
using Wabbajack.Blazor.Components;

namespace Wabbajack.Blazor.Test;

// HomePage renders its static welcome + get-started regardless of the (fire-and-forget) modlist load.
[NotInParallel] // shares process-global MessageBus.Current / RxApp scheduler
public class HomeTests
{
    [Test]
    public async Task HomePage_RendersWelcomeAndGetStarted()
    {
        using var ctx = new BunitContext();
        TestSupport.Register(ctx.Services);

        var cut = ctx.Render<HomePage>();

        await Assert.That(cut.Markup).Contains("Welcome to");
        await Assert.That(cut.Markup).Contains("Get Started");
    }
}
