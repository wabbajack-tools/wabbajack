using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wabbajack.DTOs.JsonConverters;

public class VersionConverter : JsonConverter<Version>
{
    public override Version? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return new Version(reader.GetString()!);
    }

    public override void Write(Utf8JsonWriter writer, Version value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}