using System;

namespace Wabbajack.Paths.Test;

public class ExtensionTests
{
    public static Extension DDS = new(".DDS");
    public static Extension Dds = new(".Dds");
    public static Extension DDS2 = new(".DDS");
    public static Extension EMPTY = new("");

    [Test]
    public async Task ExtensionsAreEqual()
    {
        await Assert.That(DDS).IsEqualTo(DDS);
        await Assert.That(DDS2).IsEqualTo(DDS);
        await Assert.That(Dds).IsEqualTo(DDS);

        await Assert.That(DDS == Dds).IsTrue();
        await Assert.That(DDS != EMPTY).IsTrue();

        await Assert.That(DDS).IsNotEqualTo(EMPTY);

        await Assert.That(DDS.Equals((object) 42)).IsFalse();
    }

    [Test]
    public async Task CanGetExtensionOfPath()
    {
        await Assert.That(((AbsolutePath) @"c:\foo\bar.dds").Extension).IsEqualTo(DDS);
    }

    [Test]
    public async Task ExtensionsHaveConversionOperators()
    {
        await Assert.That(".DDS" == (string) DDS).IsTrue();
        await Assert.That(DDS == (Extension) ".DDs").IsTrue();
    }

    [Test]
    public async Task ExtensionsRequireDots()
    {
        await Assert.That(() => new Extension("foo")).ThrowsExactly<PathException>();
    }

    [Test]
    public async Task ExtensionsOverrideObjectMethods()
    {
        await Assert.That(DDS.ToString()).IsEqualTo(".DDS");
        await Assert.That(DDS.GetHashCode()).IsEqualTo(".DDS".GetHashCode(StringComparison.InvariantCultureIgnoreCase));
    }

    [Test]
    public async Task CanGetExtensionFromPath()
    {
        await Assert.That(Extension.FromPath("myfoo.DDS")).IsEqualTo(DDS);
        await Assert.That(Extension.FromPath("myfoo.bar.DDS")).IsEqualTo(DDS);
        await Assert.That(Extension.FromPath("baz")).IsEqualTo(EMPTY);
    }
}
