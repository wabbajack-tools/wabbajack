using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.DTOs.Directives;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Xunit;

namespace Wabbajack.Compiler.Test.CompileSteps;

/// <summary>
///     The compile steps that decide what goes into a list beyond matching archives, run as a real compile and
///     install through <see cref="ModListHarness" />: saves, disabled and always-enabled mods, separators, and
///     config files that name the author's own folders.
/// </summary>
public class CompileStepTests : IAsyncLifetime
{
    private readonly ModListHarness _harness;
    private readonly IGameLocator _locator;
    private ModList? _modlist;

    public CompileStepTests(IServiceProvider services)
    {
        _harness = services.CreateScope().ServiceProvider.GetRequiredService<ModListHarness>();
        _locator = services.GetRequiredService<IGameLocator>();
    }

    public async Task InitializeAsync()
    {
        await _harness.InstallGeneratedMod();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private RelativePath Rel(AbsolutePath path) => path.RelativeTo(_harness._source);

    private async Task<ModList> Compile(System.Action<CompilerSettings>? configure = null)
    {
        _modlist = await _harness.Compile(configure);
        Assert.NotNull(_modlist);
        return _modlist!;
    }

    private Directive? DirectiveFor(AbsolutePath source) =>
        _modlist!.Directives.SingleOrDefault(d => d.To == Rel(source));

    private async Task<AbsolutePath> SourceFile(string relative, string content)
    {
        var path = _harness._source.Combine(relative);
        path.Parent.CreateDirectory();
        await path.WriteAllTextAsync(content);
        return path;
    }

    [Fact]
    public async Task SaveFiles_AreLeftOut()
    {
        var save = await SourceFile(Rel(_harness.ProfileFolder.Combine("saves", "quicksave.ess")).ToString(), "a save");

        await Compile();

        Assert.Null(DirectiveFor(save));
    }

    /// <summary>
    ///     The tag is looked for anywhere under the MO2 folder. In the profile folder it ships with the profile;
    ///     left in the MO2 folder itself, nothing matches the tag file and the compile stops.
    /// </summary>
    [Fact]
    public async Task SaveFiles_UnderTheIncludeSavesTag_AreShippedAndInstalled()
    {
        await SourceFile(Rel(_harness.ProfileFolder.Combine(Consts.WABBAJACK_INCLUDE_SAVES)).ToString(), "");
        var save = await SourceFile(Rel(_harness.ProfileFolder.Combine("saves", "quicksave.ess")).ToString(), "a save");

        await Compile();

        Assert.IsType<InlineFile>(DirectiveFor(save));
        Assert.True(await _harness.Install());
        Assert.Equal("a save", await _harness.InstalledPath(save).ReadAllTextAsync());
    }

    [Fact]
    public async Task DisabledMod_IsLeftOutAndAnEnabledOneIsNot()
    {
        var disabled = await _harness.AddMod("Disabled Mod").AddData("readme.txt".ToRelativePath(), "disabled");
        var enabledMod = _harness.AddMod("Enabled Mod");
        enabledMod.EnabledIn.Add(_harness.ProfileFolder.FileName.ToString());
        var enabled = await enabledMod.AddData("readme.txt".ToRelativePath(), "enabled");

        await Compile();

        Assert.Null(DirectiveFor(disabled));
        Assert.NotNull(DirectiveFor(enabled));
    }

    [Fact]
    public async Task AlwaysEnabledMod_IsShippedThoughDisabledInTheProfile()
    {
        var mod = _harness.AddMod("Always On");
        var file = await mod.AddData("readme.txt".ToRelativePath(), "always");

        await Compile(s => s.AlwaysEnabled = new[] {Rel(mod.FullPath)});

        Assert.NotNull(DirectiveFor(file));
    }

    /// <summary>MO2 lists a separator as disabled ("-Name_separator"); it is a heading, not a mod to leave out.</summary>
    [Fact]
    public async Task Separator_IsShippedThoughMo2ListsItDisabled()
    {
        var separator = await _harness.AddMod("Patches_separator").AddData("meta.ini".ToRelativePath(),
            "[General]\r\nmodid=0\r\n");

        await Compile();

        Assert.NotNull(DirectiveFor(separator));
    }

    /// <summary>
    ///     A config file naming the author's game or MO2 folder is shipped with those paths swapped for markers,
    ///     in each of the three ways config files write a path, and the install writes its own folders back.
    /// </summary>
    [Fact]
    public async Task ConfigFile_NamingTheAuthorsFolders_IsRewrittenForTheInstall()
    {
        var game = _locator.GameLocation(Game.SkyrimSpecialEdition).ToString();
        var source = _harness._source.ToString();
        var mod = _harness.AddMod("Configured");
        mod.EnabledIn.Add(_harness.ProfileFolder.FileName.ToString());
        var config = await mod.AddData("settings.ini".ToRelativePath(), string.Join("\r\n",
            $"gameBack={game}\\Data",
            $"gameDouble={game.Replace("\\", "\\\\")}\\\\Data",
            $"gameForward={game.Replace("\\", "/")}/Data",
            $"gameUpper={game.ToUpperInvariant()}\\Data",
            $"gameWine={_locator.GameLocation(Game.SkyrimSpecialEdition).ToWindowsProgramPath()}\\Data",
            $"mo2Back={source}\\mods",
            $"mo2Forward={source.Replace("\\", "/")}/mods",
            "plain=value"));

        await Compile();

        Assert.IsType<RemappedInlineFile>(DirectiveFor(config));
        Assert.True(await _harness.Install());
        var installGame = AsWritten(_harness.InstallGameFolder);
        var install = AsWritten(_harness.InstalledPath(_harness._source));
        var lines = (await _harness.InstalledPath(config).ReadAllTextAsync()).Split("\r\n");
        Assert.Equal(new[]
        {
            $"gameBack={installGame}\\Data",
            $"gameDouble={installGame.Replace("\\", "\\\\")}\\\\Data",
            $"gameForward={installGame.Replace("\\", "/")}/Data",
            $"gameUpper={installGame}\\Data",
            $"gameWine={_harness.InstallGameFolder.ToWindowsProgramPath()}\\Data",
            $"mo2Back={install}\\mods",
            $"mo2Forward={install.Replace("\\", "/")}/mods",
            "plain=value"
        }, lines);
    }

    /// <summary>
    ///     A config file naming the author's downloads folder points at the downloads folder the user chose. It
    ///     used to keep the author's path: the step swapped the MO2 folder in where it meant the downloads one.
    /// </summary>
    [Fact]
    public async Task ConfigFile_NamingTheDownloadsFolder_PointsAtTheInstallsDownloads()
    {
        var downloads = _harness.DownloadPath("x").Parent;

        var mod = _harness.AddMod("Configured");
        mod.EnabledIn.Add(_harness.ProfileFolder.FileName.ToString());
        var config = await mod.AddData("tool.ini".ToRelativePath(), string.Join("\r\n",
            $"archiveBack={downloads}\\SkyUI.7z",
            $"archiveForward={downloads.ToString().Replace("\\", "/")}/SkyUI.7z"));

        await Compile(s => s.Downloads = downloads);

        Assert.True(await _harness.Install());
        var installDownloads = AsWritten(_harness.InstallDownloadPath("x").Parent);
        Assert.Equal(new[]
        {
            $"archiveBack={installDownloads}\\SkyUI.7z",
            $"archiveForward={installDownloads.Replace("\\", "/")}/SkyUI.7z"
        }, (await _harness.InstalledPath(config).ReadAllTextAsync()).Split("\r\n"));
    }

    /// <summary>
    ///     A list's .compiler_settings is read by Wabbajack itself, to compile the list again from the install, so
    ///     it gets this machine's own paths even where MO2's files get Wine's.
    /// </summary>
    [Fact]
    public async Task CompilerSettings_NamingTheAuthorsFolder_GetsTheInstallsNativePath()
    {
        var source = _harness._source.ToString();
        var mod = _harness.AddMod("Settings");
        mod.EnabledIn.Add(_harness.ProfileFolder.FileName.ToString());
        var settings = await mod.AddData("list.compiler_settings".ToRelativePath(),
            $"{{\"Source\": \"{source.Replace("\\", "\\\\")}\"}}");

        await Compile();

        Assert.True(await _harness.Install());
        var install = _harness.InstalledPath(_harness._source).ToString();
        Assert.Equal($"{{\"Source\": \"{install.Replace("\\", "\\\\")}\"}}",
            await _harness.InstalledPath(settings).ReadAllTextAsync());
    }

    /// <summary>
    ///     A folder as the install writes it into a config file. On Windows that is the folder. Elsewhere MO2 and
    ///     the game run under Wine, so it is written on Wine's root drive; and since a Unix path has no backslashes
    ///     for the compile to tell the spellings apart by, every spelling of it is shipped as the forward one.
    /// </summary>
    private static string AsWritten(AbsolutePath folder)
    {
        return OperatingSystem.IsWindows() ? folder.ToString() : folder.ToWindowsProgramPath().Replace('\\', '/');
    }

    [Fact]
    public async Task ConfigFile_NamingNoFolder_IsShippedUnchanged()
    {
        var mod = _harness.AddMod("Configured");
        mod.EnabledIn.Add(_harness.ProfileFolder.FileName.ToString());
        var config = await mod.AddData("settings.ini".ToRelativePath(), "[Display]\r\nwidth=1920\r\n");

        await Compile();

        Assert.IsNotType<RemappedInlineFile>(DirectiveFor(config));
        Assert.True(await _harness.Install());
        Assert.Equal("[Display]\r\nwidth=1920\r\n", await _harness.InstalledPath(config).ReadAllTextAsync());
    }
}
