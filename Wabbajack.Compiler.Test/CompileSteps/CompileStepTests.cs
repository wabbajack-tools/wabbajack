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
            $"mo2Back={source}\\mods",
            $"mo2Forward={source.Replace("\\", "/")}/mods",
            "plain=value"));

        await Compile();

        Assert.IsType<RemappedInlineFile>(DirectiveFor(config));
        Assert.True(await _harness.Install());
        var installGame = _harness.InstallGameFolder.ToString();
        var install = _harness.InstalledPath(_harness._source).ToString();
        var lines = (await _harness.InstalledPath(config).ReadAllTextAsync()).Split("\r\n");
        Assert.Equal(new[]
        {
            $"gameBack={installGame}\\Data",
            $"gameDouble={installGame.Replace("\\", "\\\\")}\\\\Data",
            $"gameForward={installGame.Replace("\\", "/")}/Data",
            $"gameUpper={installGame}\\Data",
            $"mo2Back={install}\\mods",
            $"mo2Forward={install.Replace("\\", "/")}/mods",
            "plain=value"
        }, lines);
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
