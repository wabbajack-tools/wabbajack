using System;
using System.Linq;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Xunit;

namespace Wabbajack.DTOs.Test.Compatibility;

/// <summary>
///     Hashes and paths appear thousands of times in a modlist, each in a fixed text form: a hash is the
///     base64 of its eight little-endian bytes, a hash path is a JSON array of that hash and the path
///     parts, and paths are backslash-joined strings. A change to any of these forms makes the installer
///     look for files under the wrong hash or name, so the forms are pinned with known values here.
/// </summary>
public class ValueFormatTests
{
    private readonly DTOSerializer _serializer;

    public ValueFormatTests(DTOSerializer serializer)
    {
        _serializer = serializer;
    }

    private class HashHolder
    {
        public Hash Hash { get; set; }
    }

    private class HashPathHolder
    {
        public HashRelativePath HashPath { get; set; }
    }

    private class RelativeHolder
    {
        public RelativePath Relative { get; set; }
    }

    private class AbsoluteHolder
    {
        public AbsolutePath Absolute { get; set; }
    }

    [Theory]
    [InlineData(0x0000000000000000UL, "AAAAAAAAAAA=")]
    [InlineData(0x0123456789ABCDEFUL, "782riWdFIwE=")]
    [InlineData(0xFFFFFFFFFFFFFFFFUL, "//////////8=")]
    [InlineData(0xA75D2B2599A7A22EUL, "LqKnmSUrXac=")]
    [InlineData(0xB27F1C0DE3C1BAFDUL, "/brB4w0cf7I=")]
    [InlineData(0xFFFBBFFFFBBFFFFBUL, "+/+/+/+/+/8=")]
    public void HashIsWrittenAsBase64OfItsLittleEndianBytes(ulong value, string base64)
    {
        var json = _serializer.Serialize(new HashHolder {Hash = Hash.FromULong(value)});

        Assert.Equal($"{{\"Hash\":\"{base64}\"}}", json);
        Assert.Equal(Hash.FromULong(value), _serializer.Deserialize<HashHolder>(json)!.Hash);
        Assert.Equal(Hash.FromULong(value), Hash.FromBase64(base64));
    }

    /// <summary>
    ///     JSON lets a writer escape any character, and System.Text.Json's own writer escapes "+". A list that
    ///     went through such a tool used to have those hashes read as something else, silently.
    /// </summary>
    [Theory]
    [InlineData("""{"Hash":"+/+/+/+/+/8="}""", 0xFFFBBFFFFBBFFFFBUL)]
    [InlineData("""{"Hash":"\/brB4w0cf7I="}""", 0xB27F1C0DE3C1BAFDUL)]
    [InlineData("""{"Hash":"LqKnmSUrXac="}""", 0xA75D2B2599A7A22EUL)]
    public void EscapedHashText_ReadsAsTheSameHash(string json, ulong value)
    {
        Assert.Equal(Hash.FromULong(value), _serializer.Deserialize<HashHolder>(json)!.Hash);
    }

    [Fact]
    public void RandomHashesRoundTrip()
    {
        var random = new Random(1234);
        for (var i = 0; i < 10_000; i++)
        {
            var hash = Hash.FromULong((ulong) random.NextInt64(long.MinValue, long.MaxValue));
            var read = _serializer.Deserialize<HashHolder>(_serializer.Serialize(new HashHolder {Hash = hash}))!;
            Assert.Equal(hash, read.Hash);
        }
    }

    [Fact]
    public void HashPathIsAnArrayOfHashThenParts()
    {
        var path = new HashRelativePath(Hash.FromULong(0xA75D2B2599A7A22E),
            (RelativePath) @"stylesheets\Night Eyes.qss", (RelativePath) @"inner\file.dds");

        var json = _serializer.Serialize(new HashPathHolder {HashPath = path});

        Assert.Equal("""{"HashPath":["LqKnmSUrXac=","stylesheets\\Night Eyes.qss","inner\\file.dds"]}""", json);
        var read = _serializer.Deserialize<HashPathHolder>(json)!.HashPath;
        Assert.Equal(path.Hash, read.Hash);
        Assert.Equal(path.Parts.Select(p => p.ToString()), read.Parts.Select(p => p.ToString()));
    }

    /// <summary>
    ///     Unlike a bare hash, the hash inside a hash path goes through the writer's string escaping, so
    ///     "+" comes out as \u002B. Its reader unescapes, so it round-trips, and the plain form reads too.
    /// </summary>
    [Fact]
    public void HashPathWithPlusAndSlashRoundTrips()
    {
        var path = new HashRelativePath(Hash.FromULong(0xFFFBBFFFFBBFFFFB), (RelativePath) "a.esp");

        var json = _serializer.Serialize(new HashPathHolder {HashPath = path});
        var read = _serializer.Deserialize<HashPathHolder>(json)!.HashPath;

        Assert.Equal("""{"HashPath":["\u002B/\u002B/\u002B/\u002B/\u002B/8=","a.esp"]}""", json);
        Assert.Equal(path.Hash, read.Hash);
        Assert.Equal(path.Hash,
            _serializer.Deserialize<HashPathHolder>("""{"HashPath": ["+/+/+/+/+/8=", "a.esp"]}""")!.HashPath.Hash);
    }

    [Fact]
    public void HashPathWithOnlyAHashReads()
    {
        var read = _serializer.Deserialize<HashPathHolder>("""{"HashPath": ["LqKnmSUrXac="]}""")!.HashPath;

        Assert.Equal(Hash.FromULong(0xA75D2B2599A7A22E), read.Hash);
        Assert.Empty(read.Parts);
    }

    [Theory]
    [InlineData(@"a.esp", @"a.esp")]
    [InlineData(@"mods\SkyUI\Interface\skyui\config.txt", @"mods\SkyUI\Interface\skyui\config.txt")]
    [InlineData(@"mods/SkyUI/Interface/skyui/config.txt", @"mods\SkyUI\Interface\skyui\config.txt")]
    [InlineData(@"\leading\and\trailing\", @"leading\and\trailing")]
    [InlineData(@"Mixed Case\With Spaces.TXT", @"Mixed Case\With Spaces.TXT")]
    [InlineData("unicode\\\u00e9t\u00e9.txt", "unicode\\\u00e9t\u00e9.txt")]
    [InlineData("", "")]
    public void RelativePathIsABackslashJoinedString(string written, string normalised)
    {
        var read = _serializer.Deserialize<RelativeHolder>(
            _serializer.Serialize(new {Relative = written}))!.Relative;

        Assert.Equal(normalised, read.ToString());
        var json = _serializer.Serialize(new RelativeHolder {Relative = read});
        Assert.Equal(_serializer.Serialize(new {Relative = normalised}), json);
        Assert.Equal(normalised, _serializer.Deserialize<RelativeHolder>(json)!.Relative.ToString());
    }

    [Fact]
    public void DefaultRelativePathIsWrittenAsAnEmptyString()
    {
        Assert.Equal("""{"Relative":""}""", _serializer.Serialize(new RelativeHolder()));
    }

    [Theory]
    [InlineData(@"C:\Modlists\Halgari's Helper", @"C:\Modlists\Halgari's Helper")]
    [InlineData(@"C:/Modlists/Helper", @"C:\Modlists\Helper")]
    [InlineData(@"D:\", @"D:")]
    [InlineData(@"\\server\share\list", @"\\server\share\list")]
    [InlineData(@"/home/user/modlists", @"/home/user/modlists")]
    public void AbsolutePathRoundTripsAsItsText(string written, string normalised)
    {
        var read = _serializer.Deserialize<AbsoluteHolder>(
            _serializer.Serialize(new {Absolute = written}))!.Absolute;

        Assert.Equal(normalised, read.ToString());
        var json = _serializer.Serialize(new AbsoluteHolder {Absolute = read});
        Assert.Equal(_serializer.Serialize(new {Absolute = normalised}), json);
        Assert.Equal(normalised, _serializer.Deserialize<AbsoluteHolder>(json)!.Absolute.ToString());
    }

    [Fact]
    public void EmptyAbsolutePathReadsAsDefault()
    {
        Assert.Equal("""{"Absolute":""}""", _serializer.Serialize(new AbsoluteHolder()));
        Assert.Equal(default, _serializer.Deserialize<AbsoluteHolder>("""{"Absolute": ""}""")!.Absolute);
    }
}
