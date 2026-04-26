using System.Text;
using System.Text.Json;
using SteamKit2;
using Wabbajack.DTOs.JsonConverters;

namespace Wabbajack.Networking.Steam;

public class KeyValueTranslator
{
    public static void Translate(Utf8JsonWriter wtr, List<KeyValue> kvs)
    {
        foreach (var kv in kvs)
        {
            wtr.WritePropertyName(kv.Name!);
            if (kv.Value == null)
            {
                wtr.WriteStartObject();
                Translate(wtr, kv.Children);
                wtr.WriteEndObject();
            }
            else
            {
                wtr.WriteStringValue(kv.Value);
            }
        }
    }
    

    public static T Translate<T>(KeyValue kv, DTOSerializer dtos)
    {
        var ms = new MemoryStream();
        var wtr = new Utf8JsonWriter(ms, new JsonWriterOptions()
        {
            Indented = true,
        });

        wtr.WriteStartObject();
        Translate(wtr, kv.Children);
        wtr.WriteEndObject();
        wtr.Flush();

        var str = Encoding.UTF8.GetString(ms.ToArray());
        
        
        return JsonSerializer.Deserialize<T>(str, dtos.Options)!;
    }
}