using System;
using System.Data.HashFunction.xxHash;
using Xunit;

namespace Wabbajack.Hashing.xxHash64.Test;

public class HashTests
{
    private static readonly Hash Hash1 = new(1);
    private static readonly Hash Hash1a = new(1);
    private static readonly Hash Hash2 = new(2);

  
    private ulong HashOld(byte[] data)
    {
        var config = new xxHashConfig {HashSizeInBits = 64};
        return BitConverter.ToUInt64(xxHashFactory.Instance.Create(config).ComputeHash(data).Hash);
    }

    [Fact]
    public void HashesAreEquatable()
    {
        Assert.Equal(Hash1, Hash1a);
        Assert.True(Hash1 == Hash1a);
        Assert.True(Hash1 != Hash2);
    }

    [Fact]
    public void HashesAreSortable()
    {
        Assert.Equal(0, Hash1.CompareTo(Hash1a));
        Assert.Equal(-1, Hash1.CompareTo(Hash2));
        Assert.Equal(1, Hash2.CompareTo(Hash1));
    }

    [Fact]
    public void HashesConvertToBase64()
    {
        Assert.Equal("AQAAAAAAAAA=", Hash1.ToBase64());
        Assert.Equal("AQAAAAAAAAA=", Hash1.ToString());
        Assert.Equal(Hash1, Hash.Interpret(Hash1.ToBase64()));
    }

    [Fact]
    public void HashesConvertToHex()
    {
        Assert.Equal("0100000000000000", Hash1.ToHex());
        Assert.Equal(Hash1, Hash.Interpret(Hash1.ToHex()));
    }

    [Fact]
    public void HashesConvertToLong()
    {
        Assert.Equal(Hash1, Hash.Interpret(((long) Hash1).ToString()));
    }

    [Fact]
    public void MiscMethods()
    {
        Assert.Equal(1, Hash1.GetHashCode());
        Assert.Equal(1, (long) Hash1);
        Assert.Equal((ulong) 1, (ulong) Hash1);
        Assert.True(Hash1.Equals(Hash1a));
        Assert.True(Hash1.Equals((object) Hash1a));
        Assert.NotEqual(Hash1, (object) 4);
        Assert.Equal(Hash1, Hash.FromULong(1));
        Assert.Equal(new byte[] {1, 0, 0, 0, 0, 0, 0, 0}, Hash1.ToArray());
    }
}