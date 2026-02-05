using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wabbajack.Paths;

namespace Wabbajack.DTOs.JsonConverters;

/// <summary>
/// JSON converter for RelativePath. Accepts both forward and backslash separators on read,
/// writes with backslashes (Windows-style for modlist compatibility).
/// </summary>
public class RelativePathConverter : JsonConverter<RelativePath>
{
    public override RelativePath Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var raw = reader.GetString();
        if (string.IsNullOrEmpty(raw)) return RelativePath.Empty;

        // The explicit cast operator handles sanitization (converts / to \)
        return (RelativePath)raw;
    }

    public override void Write(Utf8JsonWriter writer, RelativePath value, JsonSerializerOptions options)
    {
        // Write with backslashes (Windows-style for modlist compatibility)
        writer.WriteStringValue(value.Path);
    }
}
