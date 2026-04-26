using System;
using Xunit;

namespace Wabbajack.Paths.Test;

public class FullPathTests
{
    public static FullPath Foo = new(@"c:\foo.zip".ToAbsolutePath(), Array.Empty<RelativePath>());
    public static FullPath FooBar = new(@"c:\foo.zip".ToAbsolutePath(), "Bar.7z".ToRelativePath());
    public static FullPath Foobar = new(@"c:\foo.zip".ToAbsolutePath(), "bar.7z".ToRelativePath());

    [Fact]
    public void CanGetExtensions()
    {
        Assert.Equal(new Extension(".7z"), FooBar.Extension);
        Assert.Equal(new Extension(".zip"), Foo.Extension);
    }

    [Fact]
    public void CanGetFileName()
    {
        Assert.Equal("Bar.7z".ToRelativePath(), FooBar.FileName);
        Assert.Equal("foo.zip".ToRelativePath(), Foo.FileName);
    }

    [Fact]
    public void ToStringWorks()
    {
        Assert.Equal(@"c:\foo.zip|bar.7z", Foobar.ToString());
    }

    [Fact]
    public void HashCodeWorks()
    {
        Assert.Equal(FooBar.GetHashCode(), Foobar.GetHashCode());
    }

    [Fact]
    public void CompareWorks()
    {
        Assert.Equal(-1, Foo.CompareTo(FooBar));
        Assert.Equal(0, Foobar.CompareTo(FooBar));
        Assert.NotEqual(-1, new FullPath(@"z:\arr".ToAbsolutePath()).CompareTo(Foo));
    }

    [Fact]
    public void EqualityWorks()
    {
        Assert.Equal(Foobar, FooBar);
        Assert.NotEqual(new FullPath(@"z:\arr".ToAbsolutePath()), Foo);
        Assert.NotEqual(Foo, Foobar);
        Assert.NotEqual(Foo, (object) 42);

        Assert.True(FooBar == Foobar);
        Assert.True(FooBar != Foo);
    }
}