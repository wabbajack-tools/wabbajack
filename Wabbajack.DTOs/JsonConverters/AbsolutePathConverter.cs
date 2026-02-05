using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wabbajack.Paths;

namespace Wabbajack.DTOs.JsonConverters;

/// <summary>
/// JSON converter for AbsolutePath. Accepts both forward and backslash separators on read,
/// writes with forward slashes.
/// </summary>
public class AbsolutePathConverter : JsonConverter<AbsolutePath>
{
    public override AbsolutePath Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var raw = reader.GetString();
        if (string.IsNullOrEmpty(raw)) return AbsolutePath.Empty;

        // The explicit cast operator handles sanitization (converts \ to /)
        return (AbsolutePath)raw;
    }

    public override void Write(Utf8JsonWriter writer, AbsolutePath value, JsonSerializerOptions options)
    {
        // Write with forward slashes (the internal format)
        writer.WriteStringValue(value.GetFullPath());
    }
}
