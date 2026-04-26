using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wabbajack.Paths;

namespace Wabbajack.DTOs.JsonConverters;

public class RelativePathConverter : JsonConverter<RelativePath>
{
    public override RelativePath Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return (RelativePath) reader.GetString()!;
    }

    public override void Write(Utf8JsonWriter writer, RelativePath value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}