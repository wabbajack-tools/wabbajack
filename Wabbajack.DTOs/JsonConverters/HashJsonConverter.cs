using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wabbajack.Hashing.xxHash64;

namespace Wabbajack.DTOs.JsonConverters;

public class HashJsonConverter : JsonConverter<Hash>
{
    public override Hash Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return Hash.FromBase64(reader.ValueSpan);
    }

    public override void Write(Utf8JsonWriter writer, Hash value, JsonSerializerOptions options)
    {
        Span<byte> data = stackalloc byte[8];
        BitConverter.TryWriteBytes(data, (ulong) value);
        writer.WriteBase64StringValue(data);
    }
}