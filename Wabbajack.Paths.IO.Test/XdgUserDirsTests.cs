#nullable enable
using Xunit;

namespace Wabbajack.Paths.IO.Test;

public class XdgUserDirsTests
{
    private const string File = """
                                # This file is written by xdg-user-dirs-update
                                XDG_DESKTOP_DIR="$HOME/Desktop"
                                XDG_DOWNLOAD_DIR="$HOME/Téléchargements"
                                XDG_MUSIC_DIR="/mnt/data/Music/"
                                XDG_VIDEOS_DIR="Videos"
                                """;

    [Theory]
    [InlineData("DOWNLOAD", "/home/me/Téléchargements")]
    [InlineData("DESKTOP", "/home/me/Desktop")]
    [InlineData("MUSIC", "/mnt/data/Music")]
    [InlineData("VIDEOS", null)]
    [InlineData("DOCUMENTS", null)]
    public void ReadsAFolder(string name, string? expected)
    {
        Assert.Equal(expected, XdgUserDirs.Parse(File, name, "/home/me"));
    }
}
