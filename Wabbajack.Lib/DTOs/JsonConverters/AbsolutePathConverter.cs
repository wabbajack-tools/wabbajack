using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wabbajack.Paths;

namespace Wabbajack.DTOs.JsonConverters;

public class AbsolutePathConverter : JsonConverter<AbsolutePath>
{
    public override AbsolutePath Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return (AbsolutePath) reader.GetString()!;
    }

    public override void Write(Utf8JsonWriter writer, AbsolutePath value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}