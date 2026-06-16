using System.Threading.Tasks;
using Bunit;
using Wabbajack;
using Wabbajack.Blazor.Components;
using Wabbajack.Messages;

namespace Wabbajack.Blazor.Test;

[NotInParallel] // shares process-global MessageBus.Current / RxApp scheduler
public class InfoUploadTests
{
    [Test]
    public async Task Info_RendersText()
    {
        using var ctx = new BunitContext();
        TestSupport.Register(ctx.Services);

        // InfoVM picks up its text from the LoadInfoScreen message it subscribes to in its ctor, so
        // force-create the singleton (subscribing it) before sending the message.
        const string text = "hello from the info screen";
        _ = ctx.Services.GetService(typeof(InfoVM));
        LoadInfoScreen.Send(text, null);

        var cut = ctx.Render<InfoPage>();

        await Assert.That(cut.Markup).Contains(text);
    }

    [Test]
    public async Task FileUploadPanel_Renders()
    {
        using var ctx = new BunitContext();
        TestSupport.Register(ctx.Services);
        var vm = (FileUploadVM)ctx.Services.GetService(typeof(FileUploadVM))!;

        var cut = ctx.Render<FileUploadPanel>(ps => ps.Add(p => p.Vm, vm));

        await Assert.That(cut.Markup).Contains("Upload a file");
        await Assert.That(cut.Markup).Contains("Browse");
    }
}
