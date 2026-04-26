using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;

namespace Wabbajack.DTOs.JsonConverters;

public class HashRelativePathConverter : JsonConverter<HashRelativePath>
{
    public override HashRelativePath Read(ref Utf8JsonReader reader, Type typeToConvert,
        JsonSerializerOptions options)
    {
        var parts = JsonSerializer.Deserialize<string[]>(ref reader)!;
        var hash = Hash.FromBase64(parts[0]);
        return new HashRelativePath(hash,
            (parts[1..] ?? Array.Empty<string>()).Select(r => (RelativePath) r).ToArray());
    }

    public override void Write(Utf8JsonWriter writer, HashRelativePath value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        Span<byte> temp = stackalloc byte[12];
        value.Hash.ToBase64(temp);
        writer.WriteStringValue(temp);

        foreach (var part in value.Parts)
            writer.WriteStringValue(part.ToString());

        writer.WriteEndArray();
    }
}