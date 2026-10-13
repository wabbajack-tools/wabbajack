using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Wabbajack.CLI.Verbs;
using Wabbajack.Common;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Xunit;

namespace Wabbajack.CLI.Test.Offline;

/// <summary>
///     The verbs a list author and a user run, one after another, the way the command line runs them: compile an
///     MO2 folder, install what came out, and verify the install. The mod is a zip written at test time and
///     "downloaded" from <see cref="OfflineServers" />, so nothing leaves the process.
/// </summary>
public class CompileInstallVerifyTests : IClassFixture<OfflineCliHost>, IDisposable
{
    private static readonly Uri ArchiveUrl = new("https://files.example.com/mods/Test%20Mod.zip");

    private static readonly (string Name, string Content)[] ModFiles =
    {
        (@"TestMod.esp", "plugin bytes"),
        (@"meshes\test\thing.nif", "mesh bytes"),
        (@"readme.txt", "read me")
    };

    private readonly OfflineCliHost _host;
    private readonly TemporaryPath _folder;
    private readonly AbsolutePath _root;

    public CompileInstallVerifyTests(OfflineCliHost host, Xunit.Abstractions.ITestOutputHelper output)
    {
        _host = host;
        _host.Output = output;
        // The host's own manager: disposing a second one would delete the temp root the host's stubbed game
        // folder lives in.
        _folder = host.Services.GetRequiredService<TemporaryFileManager>().CreateFolder();
        _root = _folder.Path;

        // The stubbed game folder starts empty; preflight wants the files that make it a Skyrim SE install.
        var game = host.Services.GetRequiredService<IGameLocator>().GameLocation(Game.SkyrimSpecialEdition);
        foreach (var required in Game.SkyrimSpecialEdition.MetaData().RequiredFiles)
        {
            var file = game.Combine(required);
            file.Parent.CreateDirectory();
            if (!file.FileExists()) File.WriteAllText(file.ToString(), "");
        }
    }

    public void Dispose()
    {
        _folder.Dispose();
    }

    private T Verb<T>() where T : notnull => _host.Services.GetRequiredService<T>();

    private static byte[] Zip()
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
            foreach (var (name, content) in ModFiles)
            {
                var entry = zip.CreateEntry(name.Replace('\\', '/'));
                entry.LastWriteTime = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
                using var s = entry.Open();
                s.Write(Encoding.UTF8.GetBytes(content));
            }

        return ms.ToArray();
    }

    /// <summary>An MO2 folder with one mod installed from <see cref="ArchiveUrl" />, its archive in downloads.</summary>
    private async Task<(AbsolutePath Mo2, AbsolutePath Downloads)> AuthorsMo2Folder()
    {
        var mo2 = _root.Combine("Author", "MO2");
        var downloads = mo2.Combine("downloads");
        downloads.CreateDirectory();
        await mo2.Combine("ModOrganizer.ini").WriteAllTextAsync(
            "[General]\r\ngameName=Skyrim Special Edition\r\nselected_profile=@ByteArray(Default)\r\n" +
            $"[Settings]\r\ndownload_directory={downloads.ToString().Replace('\\', '/')}\r\n");
        var profile = mo2.Combine("profiles", "Default");
        profile.CreateDirectory();
        await profile.Combine("modlist.txt").WriteAllTextAsync("+Test Mod\r\n");

        var zip = Zip();
        _host.Servers.Files[ArchiveUrl] = zip;
        var archive = downloads.Combine("Test Mod.zip");
        await archive.WriteAllBytesAsync(zip);
        await archive.WithExtension(new Extension(".meta"))
            .WriteAllTextAsync($"[General]\r\ndirectURL={ArchiveUrl}\r\n");

        foreach (var (name, content) in ModFiles)
        {
            var file = mo2.Combine("mods", "Test Mod", name);
            file.Parent.CreateDirectory();
            await file.WriteAllTextAsync(content);
        }

        return (mo2, downloads);
    }

    private async Task<AbsolutePath> Compiled()
    {
        var (mo2, _) = await AuthorsMo2Folder();
        var output = _root.Combine("Out");
        output.CreateDirectory();

        Assert.Equal(0, await Verb<Compile>().Run(mo2, output, CancellationToken.None));

        return Assert.Single(output.EnumerateFiles(Ext.Wabbajack));
    }

    [Fact]
    public async Task CompileInstallVerify_RoundTrips()
    {
        var list = await Compiled();
        var install = _root.Combine("User", "Install");
        var downloads = _root.Combine("User", "Downloads");
        downloads.CreateDirectory();
        await downloads.Combine("Test Mod.zip").WriteAllBytesAsync(_host.Servers.Files[ArchiveUrl]);

        Assert.Equal(PreflightInstall.Succeeded,
            await Verb<Install>().Run(list, install, downloads, "", CancellationToken.None));

        foreach (var (name, content) in ModFiles)
            Assert.Equal(content, await install.Combine("mods", "Test Mod", name).ReadAllTextAsync());
        Assert.Equal(0, await Verb<VerifyModlistInstall>().Run(list, install, false, CancellationToken.None));
    }

    /// <summary>A user who has nothing downloaded yet: preflight fetches the archive and the install carries on.</summary>
    [Fact]
    public async Task Install_WithAnEmptyDownloadsFolder_FetchesTheArchive()
    {
        var list = await Compiled();
        var install = _root.Combine("User", "Install");
        var downloads = _root.Combine("User", "Downloads");
        downloads.CreateDirectory();

        Assert.Equal(PreflightInstall.Succeeded,
            await Verb<Install>().Run(list, install, downloads, "", CancellationToken.None));

        Assert.True(downloads.Combine("Test Mod.zip").FileExists());
        Assert.Equal("read me", await install.Combine("mods", "Test Mod", "readme.txt").ReadAllTextAsync());
    }

    [Fact]
    public async Task Verify_AnInstallWithAChangedFile_Fails()
    {
        var list = await Compiled();
        var install = _root.Combine("User", "Install");
        var downloads = _root.Combine("User", "Downloads");
        downloads.CreateDirectory();
        Assert.Equal(PreflightInstall.Succeeded,
            await Verb<Install>().Run(list, install, downloads, "", CancellationToken.None));

        await install.Combine("mods", "Test Mod", "readme.txt").WriteAllTextAsync("edited by the user");

        Assert.NotEqual(0, await Verb<VerifyModlistInstall>().Run(list, install, false, CancellationToken.None));
    }

    [Fact]
    public async Task Install_WithNothingToInstall_SaysSoAndStops()
    {
        Assert.Equal(1, await Verb<Install>().Run(default, _root.Combine("Install"), default, "", CancellationToken.None));
        Assert.Equal(1, await Verb<Install>().Run(default, _root.Combine("Install"), default, "someone/some_list",
            CancellationToken.None));
    }
}
