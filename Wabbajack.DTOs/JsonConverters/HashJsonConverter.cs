using System;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wabbajack.Hashing.xxHash64;

namespace Wabbajack.DTOs.JsonConverters;

public class HashJsonConverter : JsonConverter<Hash>
{
    public override Hash Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // ValueSpan is the raw text, escapes and all. JSON lets a writer escape any character, and
        // System.Text.Json's own writer escapes "+", so a list that went through another tool can carry
        // "+" or "\/" in a hash; decoding those bytes as base64 read a different hash, silently.
        if (reader.ValueIsEscaped)
            return Hash.FromBase64(Encoding.UTF8.GetBytes(reader.GetString()!));
        return Hash.FromBase64(reader.ValueSpan);
    }

    public override void Write(Utf8JsonWriter writer, Hash value, JsonSerializerOptions options)
    {
        Span<byte> data = stackalloc byte[8];
        BitConverter.TryWriteBytes(data, (ulong) value);
        writer.WriteBase64StringValue(data);
    }
}