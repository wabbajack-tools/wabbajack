using System.Linq;

namespace Wabbajack.Paths.Test;

public class AbsolutePathTests
{
    [Test]
    public async Task CanParsePaths()
    {
        await Assert.That(((AbsolutePath) @"c:\foo\bar").ToString()).IsEqualTo(((AbsolutePath) @"c:\foo\bar").ToString());
    }

    [Test]
    public async Task CanGetParentPath()
    {
        await Assert.That(((AbsolutePath) @"c:\foo\bar").Parent.ToString()).IsEqualTo(((AbsolutePath) @"c:\foo").ToString());
    }

    [Test]
    public async Task ParentOfTopLevelPathThrows()
    {
        await Assert.That(() => ((AbsolutePath) @"c:\").Parent.ToString()).ThrowsExactly<PathException>();
    }

    [Test]
    public async Task CanCreateRelativePathsFromAbolutePaths()
    {
        await Assert.That(((AbsolutePath) @"\\foo\bar\baz\qux.zip").RelativeTo((AbsolutePath) @"\\foo\bar")).IsEqualTo((RelativePath) @"baz\qux.zip");
        await Assert.That(() =>
            ((AbsolutePath) @"\\foo\bar\baz\qux.zip").RelativeTo((AbsolutePath) @"\\z\bar")).ThrowsExactly<PathException>();
        await Assert.That(() =>
            ((AbsolutePath) @"\\foo\bar\baz\qux.zip").RelativeTo((AbsolutePath) @"\\z\bar\buz")).ThrowsExactly<PathException>();
    }

    [Test]
    public async Task PathsAreEquatable()
    {
        await Assert.That((AbsolutePath) @"c:\foo").IsEqualTo((AbsolutePath) @"c:\foo");

        await Assert.That((AbsolutePath) @"c:\foo" == (AbsolutePath) @"c:\Foo").IsTrue();
        await Assert.That((AbsolutePath) @"c:\foo" != (AbsolutePath) @"c:\Foo").IsFalse();
        await Assert.That((AbsolutePath) @"c:\bar").IsNotEqualTo((AbsolutePath) @"c:\foo");
        await Assert.That((AbsolutePath) @"c:\foo").IsNotEqualTo((AbsolutePath) @"c:\foo\bar");
    }

    [Test]
    public async Task CanGetPathHashCodes()
    {
        await Assert.That(@"C:\Foo\Bar.bAz".ToAbsolutePath().GetHashCode()).IsEqualTo(@"c:\foo\bar.baz".ToAbsolutePath().GetHashCode());
    }


    [Test]
    public async Task CaseInsensitiveEquality()
    {
        await Assert.That(@"C:\Foo\Bar.bAz".ToAbsolutePath()).IsEqualTo(@"c:\foo\bar.baz".ToAbsolutePath());
        await Assert.That(@"c:\foo\bar.baz".ToAbsolutePath().Equals((object) 42)).IsFalse();
    }

    [Test]
    public async Task CanReplaceExtensions()
    {
        await Assert.That(((AbsolutePath) @"/foo/bar.dds").Extension).IsEqualTo(new Extension(".dds"));
        await Assert.That(((AbsolutePath) @"/foo/bar.dds").FileName).IsEqualTo((RelativePath) "bar.dds");
        await Assert.That(((AbsolutePath) @"/foo/bar.dds").ReplaceExtension(new Extension(".zip"))).IsEqualTo((AbsolutePath) @"/foo/bar.zip");
        await Assert.That(((AbsolutePath) @"/foo\bar").ReplaceExtension(new Extension(".zip"))).IsEqualTo((AbsolutePath) @"/foo\bar.zip");
    }

    [Test]
    public async Task CanGetPathFormats()
    {
        await Assert.That(((AbsolutePath) @"c:\foo\bar").PathFormat).IsEqualTo(PathFormat.Windows);
        await Assert.That(((AbsolutePath) @"\\foo\bar").PathFormat).IsEqualTo(PathFormat.Windows);
        await Assert.That(((AbsolutePath) @"/foo/bar").PathFormat).IsEqualTo(PathFormat.Unix);
        await Assert.That(() => ((AbsolutePath) @"c!\foo/bar").PathFormat).ThrowsExactly<PathException>();
    }

    [Test]
    public async Task CanCombinePaths()
    {
        await Assert.That(((AbsolutePath) "/").Combine("foo", (RelativePath) "bar", "baz/qux").ToString()).IsEqualTo("/foo/bar/baz/qux");
        await Assert.That(() => ((AbsolutePath) "/").Combine(42)).ThrowsExactly<PathException>();
    }

    [Test]
    public async Task CanConvertPathsToStrings()
    {
        await Assert.That(((AbsolutePath) "/foo/bar").ToString()).IsEqualTo("/foo/bar");
    }

    [Test]
    public async Task PathsAreComparable()
    {
        var data = new[]
        {
            (AbsolutePath) @"c:\a",
            (AbsolutePath) @"c:\b\c",
            (AbsolutePath) @"c:\d\e\f",
            (AbsolutePath) @"c:\b"
        };
        var data2 = data.OrderBy(a => a).ToArray();

        var data3 = new[]
        {
            (AbsolutePath) @"c:\a",
            (AbsolutePath) @"c:\b",
            (AbsolutePath) @"c:\b\c",
            (AbsolutePath) @"c:\d\e\f"
        };
        await Assert.That(data2.SequenceEqual(data3)).IsTrue();
    }

    [Test]
    public async Task CanGetThisAndAllParents()
    {
        var path = @"c:\foo\bar\baz.zip".ToAbsolutePath();
        var subPaths = new[]
        {
            @"c:\",
            @"C:\foo",
            @"c:\foo\Bar",
            @"c:\foo\bar\baz.zip"
        }.Select(f => f.ToAbsolutePath());

        await Assert.That(path.ThisAndAllParents().OrderBy(f => f).SequenceEqual(subPaths.OrderBy(f => f))).IsTrue();
    }
}
