#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Installer.Preflight;
using Wabbajack.Installer.Preflight.Checks;
using Wabbajack.Installer.Preflight.Rules;
using Wabbajack.Installer.Test.Preflight.Fakes;
using Wabbajack.Paths;
using Xunit;

namespace Wabbajack.Installer.Test.Preflight;

/// <summary>
///     steam-sandbox with a game from Flatpak Steam, which a game folder inside its data folder says, and
///     flatpak's own output faked.
/// </summary>
public class SteamSandboxCheckTests : IDisposable
{
    private readonly PreflightTestHost _host;
    private readonly RecordingProgress _progress = new();

    public SteamSandboxCheckTests(IServiceProvider provider)
    {
        _host = new PreflightTestHost(provider);
    }

    public void Dispose()
    {
        _host.Dispose();
    }

    private async Task<PreflightResult> Run(string? permissions)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var ctx = _host.Context();
        ctx.State.GameFolder = (SteamSandbox.FlatpakData(home) + "/.local/share/Steam/steamapps/common/Skyrim")
            .ToAbsolutePath();
        return await new SteamSandboxCheck(() => permissions).Run(ctx, _progress, CancellationToken.None);
    }

    [Fact]
    public async Task AFolderTheSandboxWasNotGiven_IsAWarningCarryingTheOverride()
    {
        if (!OperatingSystem.IsLinux()) return;

        var result = await Run("[Context]\nfilesystems=xdg-music:ro;xdg-pictures:ro;\n");

        Assert.Equal(PreflightState.Warning, result.State);
        Assert.Contains("Flatpak Steam cannot reach", result.Message);
        Assert.Contains($"flatpak override --user --filesystem=\"{_host.Config.Install}\" com.valvesoftware.Steam",
            result.Detail);
        Assert.Contains(PreflightAction.ContinueAnyway, result.Actions!);
    }

    [Fact]
    public async Task AFolderTheSandboxWasGiven_Passes()
    {
        if (!OperatingSystem.IsLinux()) return;

        var result = await Run("[Context]\nfilesystems=host;\n");

        Assert.Equal(PreflightState.Passed, result.State);
    }

    /// <summary>Not knowing is not a reason to hold anyone up.</summary>
    [Fact]
    public async Task PermissionsThatCannotBeRead_Pass()
    {
        if (!OperatingSystem.IsLinux()) return;

        var result = await Run(null);

        Assert.Equal(PreflightState.Passed, result.State);
        Assert.Contains("could not be read", result.Message);
    }
}
