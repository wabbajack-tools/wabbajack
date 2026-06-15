using System;

namespace Wabbajack.Hashing.xxHash64.Test;

public class HashTests
{
    private static readonly Hash Hash1 = new(1);
    private static readonly Hash Hash1a = new(1);
    private static readonly Hash Hash2 = new(2);

    [Test]
    public async Task HashesAreEquatable()
    {
        await Assert.That(Hash1a).IsEqualTo(Hash1);
        await Assert.That(Hash1 == Hash1a).IsTrue();
        await Assert.That(Hash1 != Hash2).IsTrue();
    }

    [Test]
    public async Task HashesAreSortable()
    {
        await Assert.That(Hash1.CompareTo(Hash1a)).IsEqualTo(0);
        await Assert.That(Hash1.CompareTo(Hash2)).IsEqualTo(-1);
        await Assert.That(Hash2.CompareTo(Hash1)).IsEqualTo(1);
    }

    [Test]
    public async Task HashesConvertToBase64()
    {
        await Assert.That(Hash1.ToBase64()).IsEqualTo("AQAAAAAAAAA=");
        await Assert.That(Hash1.ToString()).IsEqualTo("AQAAAAAAAAA=");
        await Assert.That(Hash.Interpret(Hash1.ToBase64())).IsEqualTo(Hash1);
    }

    [Test]
    public async Task HashesConvertToHex()
    {
        await Assert.That(Hash1.ToHex()).IsEqualTo("0100000000000000");
        await Assert.That(Hash.Interpret(Hash1.ToHex())).IsEqualTo(Hash1);
    }

    [Test]
    public async Task HashesConvertToLong()
    {
        await Assert.That(Hash.Interpret(((long) Hash1).ToString())).IsEqualTo(Hash1);
    }

    [Test]
    public async Task MiscMethods()
    {
        await Assert.That(Hash1.GetHashCode()).IsEqualTo(1);
        await Assert.That((long) Hash1).IsEqualTo(1);
        await Assert.That((ulong) Hash1).IsEqualTo((ulong) 1);
        await Assert.That(Hash1.Equals(Hash1a)).IsTrue();
        await Assert.That(Hash1.Equals((object) Hash1a)).IsTrue();
        await Assert.That(Hash1.Equals((object) 4)).IsFalse();
        await Assert.That(Hash.FromULong(1)).IsEqualTo(Hash1);
        await Assert.That(Hash1.ToArray().SequenceEqual(new byte[] {1, 0, 0, 0, 0, 0, 0, 0})).IsTrue();
    }
}
