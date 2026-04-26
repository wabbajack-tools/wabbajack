using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wabbajack.Paths;

namespace Wabbajack.DTOs.JsonConverters;

public class IPathConverter : JsonConverter<IPath>
{
    public override IPath? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Invalid format, expected StartObject");

        reader.Read();
        var type = reader.GetString();
        reader.Read();
        var value = reader.GetString();
        reader.Read();


        if (type == "Absolute")
            return value!.ToAbsolutePath();
        else
            return value!.ToRelativePath();
    }

    public override void Write(Utf8JsonWriter writer, IPath value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        switch (value)
        {
            case AbsolutePath a:
                writer.WriteString("Absolute", a.ToString());
                break;
            case RelativePath r:
                writer.WriteString("Relative", r.ToString());
                break;
            default:
                throw new NotImplementedException();
        }

        writer.WriteEndObject();
    }
}