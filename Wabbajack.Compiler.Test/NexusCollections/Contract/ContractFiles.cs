using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Wabbajack.Paths.IO;

namespace Wabbajack.Compiler.Test.NexusCollections.Contract;

/// <summary>
///     Reads, writes and compares the checked-in contract snapshots. Snapshots are written with sorted keys so a
///     refresh only shows what Nexus actually changed.
/// </summary>
public static class ContractFiles
{
    /// <summary>Set to 1 to have the network contract tests rewrite the snapshots from what Nexus serves now.</summary>
    public const string UpdateVariable = "NEXUS_CONTRACT_UPDATE";

    public static bool Updating => Environment.GetEnvironmentVariable(UpdateVariable) == "1";

    public static JsonObject Load(string file)
    {
        var path = KnownFolders.EntryPoint.Combine("NexusCollections", "Contract", file).ToString();
        return (JsonObject) JsonNode.Parse(File.ReadAllText(path))!;
    }

    /// <summary>Writes a snapshot into the source tree, beside this file.</summary>
    public static string WriteToSource(string file, JsonNode snapshot)
    {
        var path = Path.Combine(Path.GetDirectoryName(ThisFile())!, file);
        File.WriteAllText(path, Serialize(snapshot).ReplaceLineEndings("\r\n") + "\r\n");
        return path;
    }

    private static string ThisFile([CallerFilePath] string path = "") => path;

    public static string Serialize(JsonNode node)
    {
        return Canonical(node)!.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
    }

    private static JsonNode? Canonical(JsonNode? node)
    {
        return node switch
        {
            JsonObject obj => new JsonObject(obj.OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => KeyValuePair.Create(kv.Key, Canonical(kv.Value)))),
            JsonArray array => new JsonArray(array.Select(Canonical).ToArray()),
            _ => node?.DeepClone()
        };
    }

    /// <summary>Every place the two documents differ, as JSON pointers, for a failure message a person can act on.</summary>
    public static List<string> Diff(JsonNode? expected, JsonNode? actual, string pointer = "")
    {
        var differences = new List<string>();
        var at = pointer.Length == 0 ? "/" : pointer;
        switch (expected, actual)
        {
            case (JsonObject e, JsonObject a):
                foreach (var key in e.Select(kv => kv.Key).Union(a.Select(kv => kv.Key)).OrderBy(k => k, StringComparer.Ordinal))
                {
                    var child = $"{pointer}/{key}";
                    if (!a.ContainsKey(key)) differences.Add($"removed {child}: {Short(e[key])}");
                    else if (!e.ContainsKey(key)) differences.Add($"added {child}: {Short(a[key])}");
                    else differences.AddRange(Diff(e[key], a[key], child));
                }

                break;
            case (JsonArray e, JsonArray a) when e.Count == a.Count:
                for (var i = 0; i < e.Count; i++)
                    differences.AddRange(Diff(e[i], a[i], $"{pointer}/{i}"));
                break;
            default:
                if (!JsonNode.DeepEquals(expected, actual))
                    differences.Add($"changed {at}: {Short(expected)} -> {Short(actual)}");
                break;
        }

        return differences;
    }

    private static string Short(JsonNode? node)
    {
        var text = node?.ToJsonString() ?? "null";
        return text.Length > 160 ? text[..160] + "..." : text;
    }
}
