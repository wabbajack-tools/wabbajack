using System.Threading.Tasks;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Wabbajack;
using Wabbajack.Messages;
using Wabbajack.Paths.IO;

namespace Wabbajack.Blazor.Test;

// Guards that data-carrying navigation messages actually reach the (persistent) VM that handles them.
// These VMs subscribe in their constructors, so they MUST be singletons created before the message is
// sent — if any is reverted to transient, the message is missed and the screen shows no data
// (e.g. the installer stuck on "Loading... Please wait").
[NotInParallel] // shares process-global MessageBus.Current / RxApp scheduler
public class NavigationDataTests
{
    [Test]
    public async Task InstallationVM_IsSingleton()
    {
        using var ctx = new BunitContext();
        TestSupport.Register(ctx.Services);

        var a = ctx.Services.GetRequiredService<InstallationVM>();
        var b = ctx.Services.GetRequiredService<InstallationVM>();

        // The shell-held instance and the InstallerPage-resolved instance must be the same one.
        await Assert.That(ReferenceEquals(a, b)).IsTrue();
    }

    [Test]
    public async Task LoadModlistForInstalling_TransfersDataToInstaller()
    {
        using var ctx = new BunitContext();
        TestSupport.Register(ctx.Services);

        // Resolve the installer first (as the shell does at startup) so it is subscribed.
        var installer = ctx.Services.GetRequiredService<InstallationVM>();
        var metadata = TestSupport.FakeMetadata();
        var path = KnownFolders.EntryPoint.Combine("test.wabbajack");

        // This is exactly what a gallery item's Install() sends before navigating to the installer.
        LoadModlistForInstalling.Send(path, metadata);

        // The installer received the modlist data (handler sets these synchronously).
        for (var i = 0; i < 30 && installer.ModlistMetadata is null; i++)
            await Task.Delay(50);

        await Assert.That(installer.ModlistMetadata).IsEqualTo(metadata);
        await Assert.That(installer.WabbajackFileLocation.TargetPath).IsEqualTo(path);
    }

    [Test]
    public async Task LoadModlistForDetails_TransfersDataToDetails()
    {
        using var ctx = new BunitContext();
        TestSupport.Register(ctx.Services);

        var details = ctx.Services.GetRequiredService<ModListDetailsVM>();
        var tile = TestSupport.CreateTile(ctx.Services);

        LoadModlistForDetails.Send(tile);

        await Assert.That(ReferenceEquals(details.MetadataVM, tile)).IsTrue();
    }

    [Test]
    public async Task LoadCompilerSettings_TransfersDataToCompilerMain()
    {
        using var ctx = new BunitContext();
        TestSupport.Register(ctx.Services);

        // CompilerHome navigates to CompilerMain THEN sends LoadCompilerSettings, so CompilerMain must
        // be a pre-existing singleton subscriber.
        var compiler = ctx.Services.GetRequiredService<CompilerMainVM>();
        var b = ctx.Services.GetRequiredService<CompilerMainVM>();
        await Assert.That(ReferenceEquals(compiler, b)).IsTrue();

        LoadCompilerSettings.Send(new Wabbajack.Compiler.CompilerSettings { ModListName = "Test Compile" });

        for (var i = 0; i < 30 && compiler.Settings?.ModListName != "Test Compile"; i++)
            await Task.Delay(50);

        await Assert.That(compiler.Settings.ModListName).IsEqualTo("Test Compile");
    }
}
