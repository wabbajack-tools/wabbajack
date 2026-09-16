using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wabbajack.Paths.IO;
using Xunit;

namespace Wabbajack.Networking.Bethesda.Test;

/// <summary>
///     The <c>.ckm</c> reader. Everything it parses arrived over the network, and two of its fields - a
///     name and a size - decide where bytes are written and how many are read, so the malformed table below
///     is the point of this file rather than an afterthought. The rule throughout is that a container it
///     cannot believe throws, and that nothing is written before the whole thing has parsed.
/// </summary>
public class BtarArchiveTests
{
    [Fact]
    public void ReadsAWellFormedContainer()
    {
        var container = new BtarBuilder {Version = 1, Flags = 3}
            .With(@"data\ccBGSSSE002-ExoticArrows.bsa", "archive bytes")
            .With(@"data\ccBGSSSE002-ExoticArrows.esl", "plugin bytes")
            .Build();

        var contents = BtarArchive.Read(container);

        Assert.Equal(1, contents.Version);
        Assert.Equal(3, contents.Flags);
        Assert.Equal(new[] {"ccBGSSSE002-ExoticArrows.bsa", "ccBGSSSE002-ExoticArrows.esl"},
            contents.Entries.Select(e => e.Name));
        Assert.Equal("archive bytes", Encoding.ASCII.GetString(contents.Entries[0].Data.Span));
        Assert.Equal("plugin bytes", Encoding.ASCII.GetString(contents.Entries[1].Data.Span));
    }

    /// <summary>The raw name is kept for messages even though only the leaf is ever written to.</summary>
    [Fact]
    public void KeepsTheNameTheContainerWroteAlongsideTheOneItIsSafeToUse()
    {
        var contents = BtarArchive.Read(new BtarBuilder().With(@"data\Foo.bsa", "x").Build());

        Assert.Equal(@"data\Foo.bsa", contents.Entries[0].RawName);
        Assert.Equal("Foo.bsa", contents.Entries[0].Name);
    }

    [Fact]
    public void ReadsAnEmptyFileAsAnEmptyFileRatherThanAsTheEndOfTheContainer()
    {
        var contents = BtarArchive.Read(new BtarBuilder()
            .With(@"data\Empty.esl", Array.Empty<byte>())
            .With(@"data\After.bsa", "still here")
            .Build());

        Assert.Equal(2, contents.Entries.Count);
        Assert.Equal(0, contents.Entries[0].Data.Length);
        Assert.Equal("still here", Encoding.ASCII.GetString(contents.Entries[1].Data.Span));
    }

    /// <summary>
    ///     Every way a container can be wrong that the reader is the last thing standing between and a bad
    ///     outcome: a payload that is not a container at all, one that ran out mid-field, one that claims
    ///     more bytes than exist, and names that would put a file somewhere nobody asked for.
    /// </summary>
    public static TheoryData<string, byte[]> Malformed()
    {
        var good = new BtarBuilder().With(@"data\Foo.bsa", "0123456789").Build();

        return new TheoryData<string, byte[]>
        {
            {"empty payload", Array.Empty<byte>()},
            {"shorter than a header", "BTA"u8.ToArray()},
            {"wrong magic", new BtarBuilder {Magic = "BSA\0"u8.ToArray()}.With(@"data\Foo.bsa", "x").Build()},
            {"an HTML error page", Encoding.ASCII.GetBytes("<html><body>403 Forbidden</body></html>")},
            {"truncated mid-payload", good[..^4]},
            {"truncated mid-size-field", new BtarBuilder {Truncate = 14}.With(@"data\Foo.bsa", "0123456789").Build()},
            {"one stray byte after the header", good.Concat(new byte[] {0x41}).ToArray()},
            {"size larger than the container", new BtarBuilder().With(@"data\Foo.bsa", "short"u8.ToArray(), 1 << 20).Build()},
            {"an absurd size", new BtarBuilder().With(@"data\Foo.bsa", "short"u8.ToArray(), ulong.MaxValue).Build()},
            {"a name length past the end", new BtarBuilder {Truncate = 20}.With(@"data\AVeryLongNameIndeed.bsa", "0123456789").Build()},
            {"an empty name", new BtarBuilder().With("", "x").Build()},
            {"a name that is only separators", new BtarBuilder().With(@"data\", "x").Build()},
            {"a name that is a directory reference", new BtarBuilder().With(@"data\..", "x").Build()}
        };
    }

    [Theory]
    [MemberData(nameof(Malformed))]
    public void RefusesAContainerItCannotBelieve(string why, byte[] container)
    {
        Assert.ThrowsAny<BtarFormatException>(() => BtarArchive.Read(container));
        Assert.NotEmpty(why);
    }

    /// <summary>
    ///     A container that does hold a usable leaf under a hostile path still yields that leaf, because
    ///     the real ones all write <c>data\</c> in front of every name. The point is that the directory part
    ///     is dropped, not that a name containing one is refused.
    /// </summary>
    [Theory]
    [InlineData(@"data\Foo.bsa", "Foo.bsa")]
    [InlineData("data/Foo.bsa", "Foo.bsa")]
    [InlineData(@"..\..\..\Windows\System32\Foo.bsa", "Foo.bsa")]
    [InlineData(@"data\sub\dir\Foo.esl", "Foo.esl")]
    [InlineData("Foo.bsa", "Foo.bsa")]
    public void ReducesANameToItsLeaf(string rawName, string expected)
    {
        Assert.Equal(expected, BtarArchive.SafeName(rawName));
    }

    /// <summary>
    ///     The whole point of taking the leaf, at container level: a name aimed somewhere else lands beside
    ///     the others and nowhere near where it was pointing.
    /// </summary>
    [Fact]
    public async Task WritesANameThatPointsOutOfTheFolderInsideItAnyway()
    {
        using var temp = new TemporaryFileManager();
        using var folder = temp.CreateFolder();

        var written = await BtarArchive.ExtractTo(new BtarBuilder()
            .With(@"..\..\..\Windows\System32\Evil.dll", "payload")
            .With(@"C:\Windows\System32\AlsoEvil.dll", "payload")
            .Build(), folder.Path);

        Assert.Equal(new[] {"Evil.dll", "AlsoEvil.dll"}, written.Select(p => p.ToString()));
        Assert.Equal(2, folder.Path.EnumerateFiles().Count());
        Assert.True(folder.Path.Combine("Evil.dll").FileExists());
        Assert.True(folder.Path.Combine("AlsoEvil.dll").FileExists());
    }

    [Theory]
    [InlineData("")]
    [InlineData(@"data\")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData(@"data\..")]
    [InlineData("C:")]
    [InlineData("Foo\0.bsa")]
    public void RefusesANameThatIsNotAFileName(string rawName)
    {
        Assert.Throws<BtarFormatException>(() => BtarArchive.SafeName(rawName));
    }

    [Fact]
    public async Task ExtractsEveryEntryUnderItsLeafName()
    {
        using var temp = new TemporaryFileManager();
        using var folder = temp.CreateFolder();

        var written = await BtarArchive.ExtractTo(new BtarBuilder()
            .With(@"data\ccBGSSSE002-ExoticArrows.bsa", "archive")
            .With(@"data\ccBGSSSE002-ExoticArrows.esl", "plugin")
            .Build(), folder.Path);

        Assert.Equal(2, written.Count);
        Assert.Equal("archive",
            await folder.Path.Combine("ccBGSSSE002-ExoticArrows.bsa").ReadAllTextAsync());
        Assert.Equal("plugin",
            await folder.Path.Combine("ccBGSSSE002-ExoticArrows.esl").ReadAllTextAsync());
    }

    /// <summary>
    ///     A container that goes wrong halfway leaves nothing behind. Parsing the whole thing first is what
    ///     makes that true, and it is the difference between a failed fetch and a downloads folder holding
    ///     half a Creation that later hashes as damage.
    /// </summary>
    [Fact]
    public async Task WritesNothingWhenTheContainerIsBad()
    {
        using var temp = new TemporaryFileManager();
        using var folder = temp.CreateFolder();

        var container = new BtarBuilder()
            .With(@"data\Good.bsa", "written if anything is")
            .With(@"data\Bad.esl", "short"u8.ToArray(), 1 << 20)
            .Build();

        await Assert.ThrowsAsync<BtarFormatException>(async () =>
            await BtarArchive.ExtractTo(container, folder.Path));

        Assert.Empty(folder.Path.EnumerateFiles());
    }
}
