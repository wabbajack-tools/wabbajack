using System;

namespace Wabbajack.Paths.Test;

public class FullPathTests
{
    public static FullPath Foo = new(@"c:\foo.zip".ToAbsolutePath(), Array.Empty<RelativePath>());
    public static FullPath FooBar = new(@"c:\foo.zip".ToAbsolutePath(), "Bar.7z".ToRelativePath());
    public static FullPath Foobar = new(@"c:\foo.zip".ToAbsolutePath(), "bar.7z".ToRelativePath());

    [Test]
    public async Task CanGetExtensions()
    {
        await Assert.That(FooBar.Extension).IsEqualTo(new Extension(".7z"));
        await Assert.That(Foo.Extension).IsEqualTo(new Extension(".zip"));
    }

    [Test]
    public async Task CanGetFileName()
    {
        await Assert.That(FooBar.FileName).IsEqualTo("Bar.7z".ToRelativePath());
        await Assert.That(Foo.FileName).IsEqualTo("foo.zip".ToRelativePath());
    }

    [Test]
    public async Task ToStringWorks()
    {
        await Assert.That(Foobar.ToString()).IsEqualTo(@"c:\foo.zip|bar.7z");
    }

    [Test]
    public async Task HashCodeWorks()
    {
        await Assert.That(Foobar.GetHashCode()).IsEqualTo(FooBar.GetHashCode());
    }

    [Test]
    public async Task CompareWorks()
    {
        await Assert.That(Foo.CompareTo(FooBar)).IsEqualTo(-1);
        await Assert.That(Foobar.CompareTo(FooBar)).IsEqualTo(0);
        await Assert.That(new FullPath(@"z:\arr".ToAbsolutePath()).CompareTo(Foo)).IsNotEqualTo(-1);
    }

    [Test]
    public async Task EqualityWorks()
    {
        await Assert.That(FooBar).IsEqualTo(Foobar);
        await Assert.That(Foo).IsNotEqualTo(new FullPath(@"z:\arr".ToAbsolutePath()));
        await Assert.That(Foobar).IsNotEqualTo(Foo);
        await Assert.That(Foo.Equals((object) 42)).IsFalse();

        await Assert.That(FooBar == Foobar).IsTrue();
        await Assert.That(FooBar != Foo).IsTrue();
    }
}
