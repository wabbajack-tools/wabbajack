using Wabbajack.Paths;
using Xunit;

namespace Wabbajack.Hashing.xxHash64.Test;

public class HashRelativePathTests
{
    public HashRelativePath Path1 = new(Hash.FromLong(1), @"foo\bar.zip".ToRelativePath());

    public HashRelativePath Path1a = new(Hash.FromLong(1), @"foo\bar.zip".ToRelativePath());
    public HashRelativePath Path1Base = new(Hash.FromLong(1));
    public HashRelativePath Path1baz = new(Hash.FromLong(2), @"foo\baz.zip".ToRelativePath());
    public HashRelativePath Path2 = new(Hash.FromLong(2), @"foo\bar.zip".ToRelativePath());

    [Fact]
    public void SupportEquality()
    {
        Assert.Equal(Path1, Path1a);
        Assert.True(Path1 == Path1a);
        Assert.False(Path1 != Path1a);
        Assert.Equal(Path1, (object) Path1a);
        Assert.NotEqual(Path1, (object) 1);
        Assert.NotEqual(Path1, Path1baz);
        Assert.NotEqual(Path1, Path2);
        Assert.NotEqual(Path1, Path1Base);
    }

    [Fact]
    public void CanGetIPathMembers()
    {
        Assert.Equal(new Extension(".zip"), Path1.Extension);
        Assert.Equal("bar.zip".ToRelativePath(), Path1.FileName);
    }

    [Fact]
    public void SupportsObjectMembers()
    {
        Assert.Equal(@"AQAAAAAAAAA=|foo\bar.zip", Path1.ToString());
        Assert.Equal(Path1.GetHashCode(), Path1a.GetHashCode());
        Assert.NotEqual(Path1.GetHashCode(), Path2.GetHashCode());
    }

    [Fact]
    public void CanBeCompared()
    {
        Assert.Equal(0, Path1.CompareTo(Path1a));
        Assert.Equal(-1, Path1.CompareTo(Path2));
        Assert.Equal(1, Path2.CompareTo(Path1a));
    }
}