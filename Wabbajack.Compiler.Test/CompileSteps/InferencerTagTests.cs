using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Wabbajack.DTOs;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Xunit;

namespace Wabbajack.Compiler.Test.CompileSteps;

/// <summary>
///     How the compile page reads a list author's instructions out of an MO2 folder: tag files dropped in a
///     folder (<c>WABBAJACK_INCLUDE</c> and friends), the <c>_FILES.txt</c> lists that name paths, and tags in a
///     mod's notes. These decide what a compile includes and ignores, and an author never sees them except as
///     a list that came out wrong.
/// </summary>
public class InferencerTagTests : IDisposable
{
    private readonly TemporaryFileManager _manager;
    private readonly AbsolutePath _mo2;

    public InferencerTagTests()
    {
        _manager = new TemporaryFileManager(KnownFolders.EntryPoint.Combine(Guid.NewGuid().ToString()));
        _mo2 = _manager.CreateFolder().Path;
        Write("ModOrganizer.ini",
            "[General]\r\ngameName = Skyrim Special Edition\r\nselected_profile = @ByteArray(The List)\r\n" +
            "[Settings]\r\ndownload_directory = C:/Modlists/The List/downloads\r\n");
        Write(@"profiles\The List\modlist.txt", "+Some Mod\r\n");
    }

    public void Dispose()
    {
        _manager.Dispose();
    }

    private AbsolutePath Write(string relative, string content = "")
    {
        var path = _mo2.Combine(relative);
        path.Parent.CreateDirectory();
        File.WriteAllText(path.ToString(), content);
        return path;
    }

    private void Mod(string name, string notes = "", string comments = "")
    {
        Write($@"mods\{name}\meta.ini", $"[General]\r\nnotes={notes}\r\ncomments={comments}\r\n");
    }

    private async Task<CompilerSettings> Infer()
    {
        var settings = await new CompilerSettingsInferencer(NullLogger<CompilerSettingsInferencer>.Instance)
            .InferModListFromLocation(_mo2.Combine("profiles", "The List", "modlist.txt"));
        Assert.NotNull(settings);
        return settings!;
    }

    private static string[] Rel(params string[] paths) => paths.Select(p => p.ToRelativePath().ToString()).ToArray();
    private static string[] Of(RelativePath[] paths) => paths.Select(p => p.ToString()).OrderBy(p => p).ToArray();

    [Fact]
    public async Task ReadsTheGameSourceDownloadsAndOutputFromModOrganizerIni()
    {
        var settings = await Infer();

        Assert.Equal(Game.SkyrimSpecialEdition, settings.Game);
        Assert.Equal(_mo2, settings.Source);
        // Off Windows a C: path belongs to a Wine prefix, and no prefix has this one, so the MO2 folder's own
        // downloads folder is taken instead of a path that names nothing here.
        Assert.Equal(OperatingSystem.IsWindows() ? @"C:\Modlists\The List\downloads".ToAbsolutePath() : _mo2.Combine("downloads"),
            settings.Downloads);
        Assert.Equal(_mo2.Parent.Combine("The List.wabbajack"), settings.OutputFile);
    }

    /// <summary>MO2 leaves <c>download_directory</c> out until the user changes it from the default.</summary>
    [Fact]
    public async Task NoDownloadDirectory_MeansTheOneInsideTheMo2Folder()
    {
        Write("ModOrganizer.ini", "[General]\r\ngameName = Skyrim Special Edition\r\n[Settings]\r\n");

        var settings = await Infer();

        Assert.Equal(_mo2.Combine("downloads"), settings.Downloads);
    }

    [Theory]
    [InlineData("WABBAJACK_INCLUDE", "Include")]
    [InlineData("WABBAJACK_NOMATCH_INCLUDE", "NoMatchInclude")]
    [InlineData("WABBAJACK_IGNORE", "Ignore")]
    public async Task TagFile_MarksTheFolderItIsIn(string tag, string list)
    {
        Write($@"Tools\Thing\{tag}");

        var settings = await Infer();

        Assert.Contains(@"Tools\Thing", Of(List(settings, list)));
    }

    /// <summary>A tag in a file's name, not its contents: a file merely mentioning the tag marks nothing.</summary>
    [Fact]
    public async Task FileWhoseNameOnlyContainsATag_MarksNothing()
    {
        Write(@"Tools\Thing\WABBAJACK_INCLUDE.bak");
        Write(@"Tools\Other\notes.txt", "WABBAJACK_IGNORE");

        var settings = await Infer();

        Assert.Empty(settings.Include);
        Assert.Empty(settings.Ignore);
    }

    [Theory]
    [InlineData("WABBAJACK_IGNORE_FILES.txt")]
    [InlineData("WABBAJACK_IGNORE_FILES.TXT")]
    public async Task IgnoreFilesList_IgnoresItselfAndEveryPathItNames(string listName)
    {
        Write(@"Tools\Thing\" + listName, "cache\r\nlogs\\old.log\r\n");

        var settings = await Infer();

        Assert.Equal(Rel(@"Tools\Thing\cache", @"Tools\Thing\" + listName, @"Tools\Thing\logs\old.log").OrderBy(p => p),
            Of(settings.Ignore));
    }

    /// <summary>
    ///     A no-match list names folders to include when nothing else matches, and files, which are included
    ///     outright. Names that exist as neither are dropped.
    /// </summary>
    [Fact]
    public async Task NoMatchIncludeFilesList_SortsWhatItNamesIntoFoldersAndFiles()
    {
        Write(@"Tools\Thing\generated\a.dat");
        Write(@"Tools\Thing\settings.cfg");
        Write(@"Tools\Thing\WABBAJACK_NOMATCH_INCLUDE_FILES.txt", "generated\r\nsettings.cfg\r\nmissing\r\n");

        var settings = await Infer();

        Assert.Equal(Rel(@"Tools\Thing\WABBAJACK_NOMATCH_INCLUDE_FILES.txt", @"Tools\Thing\generated").OrderBy(p => p),
            Of(settings.NoMatchInclude));
        Assert.Equal(Rel(@"Tools\Thing\settings.cfg"), Of(settings.Include));
    }

    [Theory]
    [InlineData("WABBAJACK_ALWAYS_ENABLE", "AlwaysEnabled")]
    [InlineData("WABBAJACK_INCLUDE", "Include")]
    [InlineData("WABBAJACK_NOMATCH_INCLUDE", "NoMatchInclude")]
    [InlineData("WABBAJACK_IGNORE", "Ignore")]
    [InlineData("WABBAJACK_ALWAYS_DISABLE", "Ignore")]
    public async Task TagInAModsNotesOrComments_AppliesToTheWholeMod(string tag, string list)
    {
        Mod("By Notes", notes: $"keep this {tag} please");
        Mod("By Comments", comments: tag);
        Mod("Untagged", notes: "nothing to see");

        var settings = await Infer();

        Assert.Equal(Rel(@"mods\By Comments", @"mods\By Notes").OrderBy(p => p), Of(List(settings, list)));
    }

    [Fact]
    public async Task ModWithoutMetaIni_IsSkipped()
    {
        Write(@"mods\No Meta\plugin.esp");

        var settings = await Infer();

        Assert.Empty(settings.AlwaysEnabled);
    }

    [Fact]
    public async Task RootPath_UsesTheProfileMo2HasSelected()
    {
        var settings = await new CompilerSettingsInferencer(NullLogger<CompilerSettingsInferencer>.Instance)
            .InferFromRootPath(_mo2);

        Assert.Equal("The List", settings!.Profile);
    }

    [Fact]
    public async Task AnyFileButModlistTxt_InfersNothing()
    {
        var path = Write(@"profiles\The List\notmodlist.txt", "+Some Mod\r\n");

        var settings = await new CompilerSettingsInferencer(NullLogger<CompilerSettingsInferencer>.Instance)
            .InferModListFromLocation(path);

        Assert.Null(settings);
    }

    private static RelativePath[] List(CompilerSettings settings, string list)
    {
        return list switch
        {
            "Include" => settings.Include,
            "NoMatchInclude" => settings.NoMatchInclude,
            "Ignore" => settings.Ignore,
            "AlwaysEnabled" => settings.AlwaysEnabled,
            _ => throw new ArgumentOutOfRangeException(nameof(list))
        };
    }
}
