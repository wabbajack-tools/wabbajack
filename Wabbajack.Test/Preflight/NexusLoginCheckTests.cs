// Wabbajack.Test/Preflight/NexusLoginCheckTests.cs
using System.Linq;
using NSubstitute;
using Wabbajack.LoginManagers;
using Wabbajack.Preflight;

namespace Wabbajack.Preflight.Test;

public class NexusLoginCheckTests
{
    private INeedsLogin CreateMockNexusLogin(bool loggedIn)
    {
        var mock = Substitute.For<INeedsLogin>();
        mock.SiteName.Returns("Nexus Mods");
        mock.LoggedIn.Returns(loggedIn);
        mock.LoginFor().Returns(typeof(Wabbajack.Downloaders.NexusDownloader));
        return mock;
    }

    [Test]
    public async Task LoggedIn_Passes()
    {
        var login = CreateMockNexusLogin(true);
        var check = new NexusLoginCheck(login);

        await Assert.That(check.Status).IsEqualTo(PreflightCheckStatus.Passed);
    }

    [Test]
    public async Task NotLoggedIn_Fails()
    {
        var login = CreateMockNexusLogin(false);
        var check = new NexusLoginCheck(login);

        await Assert.That(check.Status).IsEqualTo(PreflightCheckStatus.Failed);
        await Assert.That(check.FailureMessage).Contains("Nexus");
        await Assert.That(check.ActionCommand).IsNotNull();
        await Assert.That(check.ActionLabel).IsEqualTo("Log In");
    }
}
