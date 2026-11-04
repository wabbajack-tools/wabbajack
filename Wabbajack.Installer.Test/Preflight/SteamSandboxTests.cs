#nullable enable
using Wabbajack.Installer.Preflight.Rules;
using Xunit;

namespace Wabbajack.Installer.Test.Preflight;

/// <summary>Whether a sandboxed Steam's Proton can reach a folder, decided from what the sandbox grants.</summary>
public class SteamSandboxTests
{
    private const string Home = "/home/me";

    private static string? Xdg(string name) => name switch
    {
        "DOWNLOAD" => "/home/me/Téléchargements",
        "DOCUMENTS" => "/home/me/Documents",
        _ => null
    };

    [Theory]
    [InlineData("/home/me/.var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/common/Skyrim", true, true, false, SteamSandboxKind.Flatpak)]
    [InlineData("/home/me/snap/steam/common/.local/share/Steam/steamapps/common/Skyrim", true, false, true, SteamSandboxKind.Snap)]
    [InlineData("/home/me/.local/share/Steam/steamapps/common/Skyrim", true, true, true, SteamSandboxKind.None)]
    [InlineData("/mnt/games/SteamLibrary/steamapps/common/Skyrim", false, true, false, SteamSandboxKind.Flatpak)]
    [InlineData("/mnt/games/SteamLibrary/steamapps/common/Skyrim", false, false, true, SteamSandboxKind.Snap)]
    [InlineData("/mnt/games/SteamLibrary/steamapps/common/Skyrim", true, true, false, SteamSandboxKind.None)]
    [InlineData("/mnt/games/SteamLibrary/steamapps/common/Skyrim", false, true, true, SteamSandboxKind.None)]
    public void WhichSteamTheGameCameFrom(string game, bool native, bool flatpak, bool snap, SteamSandboxKind expected)
    {
        Assert.Equal(expected, SteamSandbox.KindOf(game, Home, native, flatpak, snap));
    }

    [Fact]
    public void ReadsTheFilesystemsOutOfFlatpaksPermissions()
    {
        const string permissions = "[Context]\nshared=network;ipc;\nsockets=x11;wayland;\n" +
                                   "filesystems=xdg-music:ro;xdg-pictures:ro;/mnt/games;~/Modlists;\npersistent=.;\n";

        Assert.Equal(new[] {"xdg-music:ro", "xdg-pictures:ro", "/mnt/games", "~/Modlists"},
            SteamSandbox.FlatpakFilesystems(permissions));
    }

    [Theory]
    // Flathub's defaults: read-only music and pictures, which reach nothing an install needs.
    [InlineData("xdg-music:ro;xdg-pictures:ro", "/home/me/Modlists/Tiny", false)]
    [InlineData("home", "/home/me/Modlists/Tiny", true)]
    [InlineData("host", "/mnt/games/Lists/Tiny", true)]
    [InlineData("host-os;host-etc", "/home/me/Modlists/Tiny", false)]
    [InlineData("~/Modlists", "/home/me/Modlists/Tiny", true)]
    [InlineData("~/Modlists", "/home/me/ModlistsOld/Tiny", false)]
    [InlineData("/mnt/games", "/mnt/games/Lists/Tiny", true)]
    [InlineData("/mnt/games:ro", "/mnt/games/Lists/Tiny", false)]
    [InlineData("/mnt/games:create", "/mnt/games/Lists/Tiny", true)]
    [InlineData("!home", "/home/me/Modlists/Tiny", false)]
    [InlineData("xdg-download", "/home/me/Téléchargements/lists", true)]
    [InlineData("xdg-documents/Lists", "/home/me/Documents/Lists/Tiny", true)]
    [InlineData("xdg-documents/Lists", "/home/me/Documents/Other", false)]
    [InlineData("xdg-videos", "/home/me/Videos", false)]
    [InlineData("", "/home/me/.var/app/com.valvesoftware.Steam/Lists/Tiny", true)]
    public void WhatAFlatpakSteamCanWriteTo(string filesystems, string path, bool expected)
    {
        var granted = filesystems.Split(';', System.StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(expected, SteamSandbox.FlatpakCovers(granted, path, Home, Xdg));
    }

    [Theory]
    [InlineData("/home/me/Modlists/Tiny", true)]
    [InlineData("/home/me/.lists/Tiny", false)]
    [InlineData("/home/me/Games/.hidden/Tiny", true)]
    [InlineData("/home/me", false)]
    [InlineData("/mnt/games/Tiny", false)]
    [InlineData("/home/mel/Tiny", false)]
    public void WhatASnapSteamCanReach(string path, bool expected)
    {
        Assert.Equal(expected, SteamSandbox.SnapCovers(path, Home));
    }
}
