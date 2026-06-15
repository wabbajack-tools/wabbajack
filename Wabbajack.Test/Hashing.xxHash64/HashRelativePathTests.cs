using Wabbajack.Paths;

namespace Wabbajack.Hashing.xxHash64.Test;

public class HashRelativePathTests
{
    public HashRelativePath Path1 = new(Hash.FromLong(1), @"foo\bar.zip".ToRelativePath());

    public HashRelativePath Path1a = new(Hash.FromLong(1), @"foo\bar.zip".ToRelativePath());
    public HashRelativePath Path1Base = new(Hash.FromLong(1));
    public HashRelativePath Path1baz = new(Hash.FromLong(2), @"foo\baz.zip".ToRelativePath());
    public HashRelativePath Path2 = new(Hash.FromLong(2), @"foo\bar.zip".ToRelativePath());

    [Test]
    public async Task SupportEquality()
    {
        await Assert.That(Path1a).IsEqualTo(Path1);
        await Assert.That(Path1 == Path1a).IsTrue();
        await Assert.That(Path1 != Path1a).IsFalse();
        await Assert.That((object) Path1a).IsEqualTo(Path1);
        await Assert.That(Path1.Equals((object) 1)).IsFalse();
        await Assert.That(Path1baz).IsNotEqualTo(Path1);
        await Assert.That(Path2).IsNotEqualTo(Path1);
        await Assert.That(Path1Base).IsNotEqualTo(Path1);
    }

    [Test]
    public async Task CanGetIPathMembers()
    {
        await Assert.That(Path1.Extension).IsEqualTo(new Extension(".zip"));
        await Assert.That(Path1.FileName).IsEqualTo("bar.zip".ToRelativePath());
    }

    [Test]
    public async Task SupportsObjectMembers()
    {
        await Assert.That(Path1.ToString()).IsEqualTo(@"AQAAAAAAAAA=|foo\bar.zip");
        await Assert.That(Path1a.GetHashCode()).IsEqualTo(Path1.GetHashCode());
        await Assert.That(Path2.GetHashCode()).IsNotEqualTo(Path1.GetHashCode());
    }

    [Test]
    public async Task CanBeCompared()
    {
        await Assert.That(Path1.CompareTo(Path1a)).IsEqualTo(0);
        await Assert.That(Path1.CompareTo(Path2)).IsEqualTo(-1);
        await Assert.That(Path2.CompareTo(Path1a)).IsEqualTo(1);
    }
}
