using System.Linq;

namespace Wabbajack.Paths.Test;

public class RelativePathTests
{
    [Test]
    public async Task CanReplaceExtensions()
    {
        await Assert.That(((RelativePath) @"foo\bar.dds").Extension).IsEqualTo(new Extension(".dds"));
        await Assert.That(((RelativePath) @"foo\bar.dds").ReplaceExtension(new Extension(".zip"))).IsEqualTo((RelativePath) @"foo\bar.zip");
        await Assert.That(((RelativePath) @"foo\bar.dds").ReplaceExtension(new Extension(".zip"))).IsNotEqualTo((RelativePath) @"foo\bar\z.zip");
        await Assert.That(((RelativePath) @"foo\bar").ReplaceExtension(new Extension(".zip"))).IsEqualTo((RelativePath) @"foo\bar.zip");
    }

    [Test]
    public async Task PathsAreValidated()
    {
        await Assert.That(() => @"c:\foo".ToRelativePath()).ThrowsExactly<PathException>();
    }

    [Test]
    public async Task CanCreatePathsRelativeTo()
    {
        await Assert.That(((RelativePath) @"baz.zip").RelativeTo((AbsolutePath) @"c:\foo\bar")).IsEqualTo((AbsolutePath) @"c:\foo\bar\baz.zip");
    }

    [Test]
    public async Task ObjectMethods()
    {
        await Assert.That(((RelativePath) @"foo\bar").ToString()).IsEqualTo(@"foo\bar");

        await Assert.That((RelativePath) @"foo/bar").IsEqualTo((RelativePath) @"foo\bar");
        await Assert.That(((RelativePath) @"foo\bar").Equals((object) 42)).IsFalse();
        await Assert.That((RelativePath) @"foo\bar" == (RelativePath) @"foo/bar").IsTrue();
        await Assert.That((RelativePath) @"foo\bar" != (RelativePath) @"foo/baz").IsTrue();

        await Assert.That(((RelativePath) @"Foo\bar").GetHashCode()).IsEqualTo(((RelativePath) @"foo\bar").GetHashCode());
    }


    [Test]
    public async Task CanGetPathHashCodes()
    {
        await Assert.That(@"Foo\Bar.bAz".ToRelativePath().GetHashCode()).IsEqualTo(@"foo\bar.baz".ToRelativePath().GetHashCode());
    }


    [Test]
    public async Task CaseInsensitiveEquality()
    {
        await Assert.That(@"Foo\Bar.bAz".ToRelativePath()).IsEqualTo(@"foo\bar.baz".ToRelativePath());
        await Assert.That(@"foo\bar.baz".ToRelativePath().Equals((object) 42)).IsFalse();
    }

    [Test]
    public async Task CanGetFilenameFromRelativePath()
    {
        await Assert.That(@"foo\bar.dds".ToRelativePath().FileName).IsEqualTo((RelativePath) "bar.dds");
    }

    [Test]
    public async Task PathsAreComparable()
    {
        var data = new[]
        {
            (RelativePath) @"a",
            (RelativePath) @"b\c",
            (RelativePath) @"d\e\f",
            (RelativePath) @"b"
        };
        var data2 = data.OrderBy(a => a).ToArray();

        var data3 = new[]
        {
            (RelativePath) @"a",
            (RelativePath) @"b",
            (RelativePath) @"b\c",
            (RelativePath) @"d\e\f"
        };
        await Assert.That(data2.SequenceEqual(data3)).IsTrue();
    }
}
