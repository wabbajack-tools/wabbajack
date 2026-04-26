using System;
using System.Buffers.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wabbajack.DTOs.Texture;

namespace Wabbajack.DTOs.JsonConverters;

public class PHashConverter : JsonConverter<PHash>
{
    public override PHash Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var data = new byte[40];
        Base64.DecodeFromUtf8(reader.ValueSpan, data, out var _, out var _);
        return new PHash(data);
    }

    public override void Write(Utf8JsonWriter writer, PHash value, JsonSerializerOptions options)
    {
        writer.WriteBase64StringValue(value.Data);
    }
}