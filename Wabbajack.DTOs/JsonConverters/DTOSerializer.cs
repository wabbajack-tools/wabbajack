using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Wabbajack.DTOs.JsonConverters;

public class DTOSerializer
{
    public readonly JsonSerializerOptions Options;

    public DTOSerializer(IEnumerable<JsonConverter> converters)
    {
        Options = new JsonSerializerOptions();
        Options.NumberHandling = JsonNumberHandling.AllowReadingFromString;
        Options.ReadCommentHandling = JsonCommentHandling.Skip;
        Options.AllowTrailingCommas = true;
        foreach (var c in converters) Options.Converters.Add(c);
    }

    public T? Deserialize<T>(string text)
    {
        return JsonSerializer.Deserialize<T>(text, Options);
    }

    public ValueTask<T?> DeserializeAsync<T>(Stream stream, CancellationToken? token = null)
    {
        return JsonSerializer.DeserializeAsync<T>(stream, Options, token ?? CancellationToken.None);
    }

    public string Serialize<T>(T data, bool writeIndented = false)
    {
        var options = Options;
        if (writeIndented)
            options = new JsonSerializerOptions(Options)
            {
                WriteIndented = true
            };

        return JsonSerializer.Serialize(data, options);
    }

    public async Task Serialize<T>(T data, Stream of, bool writeIndented = false)
    {
        var options = Options;
        if (writeIndented)
            options = new JsonSerializerOptions(Options)
            {
                WriteIndented = true
            };

        await JsonSerializer.SerializeAsync(of, data, options);
    }
}