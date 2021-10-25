using System.Text.Json.Serialization;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Hashing.xxHash64;
using Xunit;

namespace Wabbajack.DTOs.Test;

public class ValueTests
{
    private readonly DTOSerializer _dtos;

    public ValueTests()
    {
        _dtos = new DTOSerializer(new JsonConverter[] {new HashJsonConverter(), new HashRelativePathConverter()});
    }

    [Fact]
    public void TestHash()
    {
        var a = new HashData {Value = Hash.FromULong(int.MaxValue)};
        var b = _dtos.Deserialize<HashData>(_dtos.Serialize(a));
        Assert.Equal(a.Value, b.Value);
    }

    [Fact]
    public void TestHashRelative()
    {
        var a = new HashDataRelative {Value = new HashRelativePath(Hash.FromULong(int.MaxValue))};
        var b = _dtos.Deserialize<HashDataRelative>(_dtos.Serialize(a));
        Assert.Equal(a.Value.Hash, b.Value.Hash);
    }

    [Fact]
    public void TestToFromJsonHash()
    {
        for (ulong hash = 0; hash < 1024 * 1024; hash++)
        {
            var a = new BoxedHash {Hash = Hash.FromULong(hash)};
            var b = _dtos.Deserialize<BoxedHash>(_dtos.Serialize(a))!;
            Assert.Equal($"{{\"Hash\":\"{a.Hash.ToString()}\"}}", _dtos.Serialize(b));
            Assert.Equal(a.Hash, Hash.FromBase64(Hash.FromULong(hash).ToBase64()));
            Assert.Equal(a.Hash, b.Hash);
        }
    }

    public class HashData
    {
        public Hash Value { get; set; }
    }

    public class HashDataRelative
    {
        public HashRelativePath Value { get; set; }
    }

    private class BoxedHash
    {
        [JsonPropertyName("Hash")] public Hash Hash { get; set; }
    }
}