using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Wabbajack.Compiler.Test.NexusCollections.Contract;

/// <summary>
///     The part of Nexus Mods' published v3 OpenAPI document (https://api.nexusmods.com/openapi.yaml) that
///     Wabbajack's collection upload uses: those operations and every schema they reach. The snapshot of it in
///     <c>nexus-v3-openapi.json</c> is what <see cref="FakeNexus" /> checks requests against and shapes its answers
///     by, and the network tests compare it with the live document. Prose is dropped, so a reworded description is
///     not a contract change; <c>x-badges</c> are kept, since Nexus announces coming requirements in them.
/// </summary>
public static class OpenApiContract
{
    public const string SnapshotFile = "nexus-v3-openapi.json";
    public static readonly Uri LiveDocument = new("https://api.nexusmods.com/openapi.yaml");

    /// <summary>The operations <c>NexusCollectionUploader</c> calls, as "METHOD /path" under the v3 server.</summary>
    public static readonly string[] Operations =
    {
        "POST /uploads/multipart",
        "GET /uploads/{id}",
        "POST /uploads/{id}/finalise",
        "POST /collections",
        "POST /collections/{id}/revisions"
    };

    private static readonly HashSet<string> Prose = new() {"description", "summary", "example", "examples", "tags"};

    /// <summary>The subset of a whole OpenAPI document this contract covers.</summary>
    public static JsonObject Extract(JsonNode document)
    {
        var operations = new JsonObject();
        var referenced = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var operation in Operations)
        {
            var (method, path) = Split(operation);
            var node = document["paths"]?[path]?[method.ToLowerInvariant()]
                       ?? throw new InvalidDataException($"The OpenAPI document no longer has {operation}");
            var stripped = StripProse(node)!;
            CollectRefs(stripped, referenced);
            operations[operation] = stripped;
        }

        var schemas = document["components"]?["schemas"] as JsonObject
                      ?? throw new InvalidDataException("The OpenAPI document has no components.schemas");
        var included = new SortedDictionary<string, JsonNode>(StringComparer.Ordinal);
        var pending = new Queue<string>(referenced);
        while (pending.Count > 0)
        {
            var name = pending.Dequeue();
            if (included.ContainsKey(name)) continue;
            var schema = StripProse(schemas[name] ?? throw new InvalidDataException($"Schema {name} is referenced but missing"))!;
            included[name] = schema;
            var more = new SortedSet<string>(StringComparer.Ordinal);
            CollectRefs(schema, more);
            foreach (var next in more) pending.Enqueue(next);
        }

        var schemaObject = new JsonObject();
        foreach (var (name, schema) in included) schemaObject[name] = schema;

        return new JsonObject
        {
            ["source"] = LiveDocument.ToString(),
            ["version"] = document["info"]?["version"]?.DeepClone(),
            ["server"] = document["servers"]?[0]?["url"]?.DeepClone(),
            ["operations"] = operations,
            ["schemas"] = schemaObject
        };
    }

    public static (string Method, string Path) Split(string operation)
    {
        var space = operation.IndexOf(' ');
        return (operation[..space], operation[(space + 1)..]);
    }

    /// <param name="propertyMap">
    ///     True for the value of a <c>properties</c> keyword, whose keys are field names - a collection's
    ///     <c>summary</c> and <c>description</c> among them - rather than keywords.
    /// </param>
    private static JsonNode? StripProse(JsonNode? node, bool propertyMap = false)
    {
        switch (node)
        {
            case JsonObject obj:
                var copy = new JsonObject();
                foreach (var (key, value) in obj.OrderBy(kv => kv.Key, StringComparer.Ordinal))
                    if (propertyMap || !Prose.Contains(key))
                        copy[key] = StripProse(value, !propertyMap && key == "properties");
                return copy;
            case JsonArray array:
                return new JsonArray(array.Select(item => StripProse(item)).ToArray());
            default:
                return node?.DeepClone();
        }
    }

    private static void CollectRefs(JsonNode? node, ISet<string> into)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (key, value) in obj)
                    if (key == "$ref" && value?.GetValue<string>() is { } r && r.StartsWith("#/components/schemas/"))
                        into.Add(r["#/components/schemas/".Length..]);
                    else
                        CollectRefs(value, into);
                break;
            case JsonArray array:
                foreach (var item in array) CollectRefs(item, into);
                break;
        }
    }

    /// <summary>Loads a YAML document as JSON, typing plain scalars the way YAML 1.2 core does.</summary>
    public static JsonNode FromYaml(string yaml)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));
        return Convert(stream.Documents[0].RootNode)!;
    }

    private static JsonNode? Convert(YamlNode node)
    {
        switch (node)
        {
            case YamlMappingNode map:
                var obj = new JsonObject();
                foreach (var (key, value) in map.Children)
                    obj[((YamlScalarNode) key).Value!] = Convert(value);
                return obj;
            case YamlSequenceNode seq:
                return new JsonArray(seq.Children.Select(Convert).ToArray());
            case YamlScalarNode scalar:
                var text = scalar.Value ?? "";
                if (scalar.Style is ScalarStyle.Plain)
                {
                    if (text is "null" or "~" or "") return null;
                    if (text is "true") return JsonValue.Create(true);
                    if (text is "false") return JsonValue.Create(false);
                    if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l)) return JsonValue.Create(l);
                    if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return JsonValue.Create(d);
                }

                return JsonValue.Create(text);
            default:
                throw new InvalidDataException($"Unexpected YAML node {node.NodeType}");
        }
    }
}

/// <summary>
///     Checks a JSON value against an OpenAPI 3.0 schema from the contract snapshot. Covers what Nexus' schemas
///     use: <c>$ref</c>, <c>allOf</c>, <c>anyOf</c>, types, <c>nullable</c>, <c>required</c>, <c>enum</c>, string
///     lengths and patterns. A property the schema does not list is reported too: Nexus does not promise to ignore
///     one, and a field we send under a name the spec no longer has is exactly the drift these tests look for.
/// </summary>
public class OpenApiValidator
{
    private readonly JsonObject _schemas;

    public OpenApiValidator(JsonObject contract)
    {
        _schemas = (JsonObject) contract["schemas"]!;
    }

    public List<string> Validate(JsonNode? value, JsonNode schema, string pointer = "")
    {
        var errors = new List<string>();
        Check(value, schema, pointer, errors, strictProperties: true);
        return errors;
    }

    public List<string> ValidateSchemaRef(JsonNode? value, string schemaName)
    {
        return Validate(value, new JsonObject {["$ref"] = "#/components/schemas/" + schemaName});
    }

    private JsonNode Resolve(JsonNode schema)
    {
        while (schema["$ref"]?.GetValue<string>() is { } r)
            schema = _schemas[r["#/components/schemas/".Length..]]
                     ?? throw new InvalidDataException($"Unknown schema {r}");
        return schema;
    }

    /// <summary>Every property name an object schema allows, following <c>allOf</c>.</summary>
    private HashSet<string> AllowedProperties(JsonNode schema)
    {
        schema = Resolve(schema);
        var names = new HashSet<string>(StringComparer.Ordinal);
        if (schema["properties"] is JsonObject props)
            foreach (var (name, _) in props) names.Add(name);
        if (schema["allOf"] is JsonArray all)
            foreach (var part in all) names.UnionWith(AllowedProperties(part!));
        return names;
    }

    private void Check(JsonNode? value, JsonNode schema, string pointer, List<string> errors, bool strictProperties)
    {
        schema = Resolve(schema);
        var at = pointer.Length == 0 ? "/" : pointer;

        if (value is null)
        {
            if (schema["nullable"]?.GetValue<bool>() != true && schema["type"] != null)
                errors.Add($"{at}: null is not allowed");
            return;
        }

        if (schema["allOf"] is JsonArray allOf)
        {
            var allowed = AllowedProperties(schema);
            foreach (var part in allOf)
                Check(value, part!, pointer, errors, strictProperties: false);
            CheckObject(value, schema, pointer, errors, strictProperties: false);
            if (strictProperties && value is JsonObject obj)
                foreach (var (name, _) in obj)
                    if (!allowed.Contains(name))
                        errors.Add($"{pointer}/{name}: not in the schema");
            return;
        }

        if (schema["anyOf"] is JsonArray anyOf)
        {
            var attempts = anyOf.Select(option =>
            {
                var e = new List<string>();
                Check(value, option!, pointer, e, strictProperties: false);
                return e;
            }).ToList();
            if (attempts.All(e => e.Count > 0))
                errors.Add($"{at}: matches none of anyOf ({string.Join("; ", attempts.SelectMany(e => e))})");
            return;
        }

        var type = schema["type"]?.GetValue<string>();
        if (type == null && (schema["properties"] != null || schema["required"] != null))
            type = "object";
        switch (type)
        {
            case "object":
                if (value is not JsonObject)
                {
                    errors.Add($"{at}: expected an object");
                    return;
                }

                CheckObject(value, schema, pointer, errors, strictProperties);
                break;
            case "array":
                if (value is not JsonArray array)
                {
                    errors.Add($"{at}: expected an array");
                    return;
                }

                for (var i = 0; i < array.Count; i++)
                    Check(array[i], schema["items"]!, $"{pointer}/{i}", errors, strictProperties);
                break;
            case "string":
                if (value is not JsonValue sv || !sv.TryGetValue<string>(out var s))
                {
                    errors.Add($"{at}: expected a string");
                    return;
                }

                if (schema["minLength"]?.GetValue<long>() is { } min && s.Length < min)
                    errors.Add($"{at}: shorter than {min}");
                if (schema["maxLength"]?.GetValue<long>() is { } max && s.Length > max)
                    errors.Add($"{at}: longer than {max}");
                if (schema["pattern"]?.GetValue<string>() is { } pattern && !Regex.IsMatch(s, pattern))
                    errors.Add($"{at}: does not match {pattern}");
                if (schema["format"]?.GetValue<string>() == "uuid" && !Guid.TryParse(s, out _))
                    errors.Add($"{at}: not a uuid");
                break;
            case "integer":
                if (value is not JsonValue iv || !(iv.TryGetValue<long>(out _) || iv.TryGetValue<int>(out _)))
                    errors.Add($"{at}: expected an integer");
                break;
            case "number":
                if (value is not JsonValue nv || !nv.TryGetValue<double>(out _))
                    errors.Add($"{at}: expected a number");
                break;
            case "boolean":
                if (value is not JsonValue bv || !bv.TryGetValue<bool>(out _))
                    errors.Add($"{at}: expected a boolean");
                break;
        }

        if (schema["enum"] is JsonArray options &&
            !options.Any(o => JsonNode.DeepEquals(o, value)))
            errors.Add($"{at}: {value.ToJsonString()} is not one of {options.ToJsonString()}");
    }

    private void CheckObject(JsonNode value, JsonNode schema, string pointer, List<string> errors, bool strictProperties)
    {
        if (value is not JsonObject obj) return;
        if (schema["required"] is JsonArray required)
            foreach (var name in required.Select(r => r!.GetValue<string>()))
                if (!obj.ContainsKey(name))
                    errors.Add($"{pointer}/{name}: required but missing");

        var props = schema["properties"] as JsonObject;
        foreach (var (name, child) in obj)
        {
            if (props?[name] is { } childSchema)
                Check(child, childSchema, $"{pointer}/{name}", errors, strictProperties: true);
            else if (strictProperties && schema["allOf"] == null)
                errors.Add($"{pointer}/{name}: not in the schema");
        }
    }
}
