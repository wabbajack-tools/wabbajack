using System.Text.Json.Serialization;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Hashing.xxHash64;

namespace Wabbajack.DTOs.Test;

public class ValueTests
{
    private readonly DTOSerializer _dtos;

    public ValueTests()
    {
        _dtos = new DTOSerializer(new JsonConverter[] {new HashJsonConverter(), new HashRelativePathConverter()});
    }

    [Test]
    public async Task TestHash()
    {
        var a = new HashData {Value = Hash.FromULong(int.MaxValue)};
        var b = _dtos.Deserialize<HashData>(_dtos.Serialize(a));
        await Assert.That(b.Value).IsEqualTo(a.Value);
    }

    [Test]
    public async Task TestHashRelative()
    {
        var a = new HashDataRelative {Value = new HashRelativePath(Hash.FromULong(int.MaxValue))};
        var b = _dtos.Deserialize<HashDataRelative>(_dtos.Serialize(a));
        await Assert.That(b.Value.Hash).IsEqualTo(a.Value.Hash);
    }

    [Test]
    public async Task TestToFromJsonHash()
    {
        for (ulong hash = 0; hash < 1024 * 1024; hash++)
        {
            var a = new BoxedHash {Hash = Hash.FromULong(hash)};
            var b = _dtos.Deserialize<BoxedHash>(_dtos.Serialize(a))!;
            await Assert.That(_dtos.Serialize(b)).IsEqualTo($"{{\"Hash\":\"{a.Hash.ToString()}\"}}");
            await Assert.That(Hash.FromBase64(Hash.FromULong(hash).ToBase64())).IsEqualTo(a.Hash);
            await Assert.That(b.Hash).IsEqualTo(a.Hash);
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