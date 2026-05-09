// Wabbajack.Test/Preflight/NexusLoginCheckTests.cs
using System.Linq;
using NSubstitute;
using Wabbajack.LoginManagers;
using Wabbajack.Preflight;
using Xunit;

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

    [Fact]
    public void LoggedIn_Passes()
    {
        var login = CreateMockNexusLogin(true);
        var check = new NexusLoginCheck(login);

        Assert.Equal(PreflightCheckStatus.Passed, check.Status);
    }

    [Fact]
    public void NotLoggedIn_Fails()
    {
        var login = CreateMockNexusLogin(false);
        var check = new NexusLoginCheck(login);

        Assert.Equal(PreflightCheckStatus.Failed, check.Status);
        Assert.Contains("Nexus", check.FailureMessage);
        Assert.NotNull(check.ActionCommand);
        Assert.Equal("Log In", check.ActionLabel);
    }
}
