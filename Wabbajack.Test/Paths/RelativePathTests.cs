using System.Linq;
using Xunit;

namespace Wabbajack.Paths.Test;

public class RelativePathTests
{
    [Fact]
    public void CanReplaceExtensions()
    {
        Assert.Equal(new Extension(".dds"), ((RelativePath) @"foo\bar.dds").Extension);
        Assert.Equal((RelativePath) @"foo\bar.zip",
            ((RelativePath) @"foo\bar.dds").ReplaceExtension(new Extension(".zip")));
        Assert.NotEqual((RelativePath) @"foo\bar\z.zip",
            ((RelativePath) @"foo\bar.dds").ReplaceExtension(new Extension(".zip")));
        Assert.Equal((RelativePath) @"foo\bar.zip",
            ((RelativePath) @"foo\bar").ReplaceExtension(new Extension(".zip")));
    }

    [Fact]
    public void PathsAreValidated()
    {
        Assert.Throws<PathException>(() => @"c:\foo".ToRelativePath());
    }

    [Fact]
    public void CanCreatePathsRelativeTo()
    {
        Assert.Equal((AbsolutePath) @"c:\foo\bar\baz.zip",
            ((RelativePath) @"baz.zip").RelativeTo((AbsolutePath) @"c:\foo\bar"));
    }

    [Fact]
    public void ObjectMethods()
    {
        Assert.Equal(@"foo\bar", ((RelativePath) @"foo\bar").ToString());

        Assert.Equal((RelativePath) @"foo\bar", (RelativePath) @"foo/bar");
        Assert.NotEqual((RelativePath) @"foo\bar", (object) 42);
        Assert.True((RelativePath) @"foo\bar" == (RelativePath) @"foo/bar");
        Assert.True((RelativePath) @"foo\bar" != (RelativePath) @"foo/baz");

        Assert.Equal(((RelativePath) @"foo\bar").GetHashCode(), ((RelativePath) @"Foo\bar").GetHashCode());
    }


    [Fact]
    public void CanGetPathHashCodes()
    {
        Assert.Equal(@"foo\bar.baz".ToRelativePath().GetHashCode(), @"Foo\Bar.bAz".ToRelativePath().GetHashCode());
    }


    [Fact]
    public void CaseInsensitiveEquality()
    {
        Assert.Equal(@"foo\bar.baz".ToRelativePath(), @"Foo\Bar.bAz".ToRelativePath());
        Assert.NotEqual(@"foo\bar.baz".ToRelativePath(), (object) 42);
    }

    [Fact]
    public void CanGetFilenameFromRelativePath()
    {
        Assert.Equal((RelativePath) "bar.dds", @"foo\bar.dds".ToRelativePath().FileName);
    }

    [Fact]
    public void PathsAreComparable()
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
        Assert.Equal(data3, data2);
    }
}