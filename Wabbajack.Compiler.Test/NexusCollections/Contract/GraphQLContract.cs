using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using GraphQLParser;
using GraphQLParser.AST;

namespace Wabbajack.Compiler.Test.NexusCollections.Contract;

/// <summary>
///     The part of the Nexus Mods GraphQL schema (https://api.nexusmods.com/v2/graphql) that Wabbajack's queries
///     touch: every field they select, with its type and every argument the field accepts. It is built from an
///     introspection of the live schema and the queries in <see cref="Networking.NexusApi.NexusGraphQL" />, and
///     kept as <c>nexus-v2-graphql.json</c>. Some fields (<c>myCollections</c>) only appear to a signed-in
///     user, so the snapshot is refreshed with a login.
/// </summary>
public static class GraphQLContract
{
    public const string SnapshotFile = "nexus-v2-graphql.json";
    public static readonly Uri LiveEndpoint = new("https://api.nexusmods.com/v2/graphql");

    public const string IntrospectionQuery = @"
        query IntrospectionQuery {
          __schema {
            queryType { name }
            types {
              kind
              name
              fields(includeDeprecated: true) {
                name
                isDeprecated
                deprecationReason
                args { name defaultValue type { ...TypeRef } }
                type { ...TypeRef }
              }
            }
          }
        }
        fragment TypeRef on __Type {
          kind name
          ofType { kind name ofType { kind name ofType { kind name ofType { kind name } } } }
        }";

    /// <summary>Renders an introspection type reference the way GraphQL writes it, e.g. <c>[Mod!]!</c>.</summary>
    public static string TypeName(JsonNode? type)
    {
        return type?["kind"]?.GetValue<string>() switch
        {
            "NON_NULL" => TypeName(type["ofType"]) + "!",
            "LIST" => "[" + TypeName(type["ofType"]) + "]",
            _ => type?["name"]?.GetValue<string>() ?? "?"
        };
    }

    public static string Unwrap(string type)
    {
        return type.Replace("[", "").Replace("]", "").Replace("!", "");
    }

    /// <summary>
    ///     The contract subset of an introspection result for these queries. Throws when a query selects a field
    ///     the schema does not have, which is the change these tests exist to notice.
    /// </summary>
    public static JsonObject Extract(JsonNode introspection, IReadOnlyDictionary<string, string> operations)
    {
        var schema = introspection["data"]?["__schema"] ?? introspection["__schema"]
                     ?? throw new InvalidDataException("Not an introspection result");
        var types = ((JsonArray) schema["types"]!).ToDictionary(t => t!["name"]!.GetValue<string>(), t => t!);
        var root = schema["queryType"]!["name"]!.GetValue<string>();

        var subset = new SortedDictionary<string, SortedDictionary<string, JsonNode>>(StringComparer.Ordinal);
        var problems = new List<string>();

        void Walk(string typeName, GraphQLSelectionSet set, string op)
        {
            if (!types.TryGetValue(typeName, out var type))
            {
                problems.Add($"{op}: type {typeName} is not in the schema");
                return;
            }

            foreach (var field in set.Selections.OfType<GraphQLField>())
            {
                var name = field.Name.StringValue;
                if (name == "__typename") continue;
                var def = (type["fields"] as JsonArray)?.FirstOrDefault(f => f!["name"]!.GetValue<string>() == name);
                if (def == null)
                {
                    problems.Add($"{op}: {typeName}.{name} is not in the schema");
                    continue;
                }

                var args = new JsonObject();
                foreach (var arg in ((JsonArray) def["args"]!).OrderBy(a => a!["name"]!.GetValue<string>(), StringComparer.Ordinal))
                    args[arg!["name"]!.GetValue<string>()] = new JsonObject
                    {
                        ["type"] = TypeName(arg["type"]),
                        ["hasDefault"] = arg["defaultValue"] != null
                    };

                var fieldType = TypeName(def["type"]);
                var entries = subset.TryGetValue(typeName, out var existing)
                    ? existing
                    : subset[typeName] = new SortedDictionary<string, JsonNode>(StringComparer.Ordinal);
                var entry = new JsonObject
                {
                    ["type"] = fieldType,
                    ["deprecated"] = def["isDeprecated"]?.GetValue<bool>() ?? false,
                    ["args"] = args
                };
                // Nexus says here what to move to; the reason is kept so a refreshed snapshot tells whoever reads it.
                if (def["deprecationReason"]?.GetValue<string>() is { Length: > 0 } reason)
                    entry["deprecationReason"] = reason;
                entries[name] = entry;

                if (field.SelectionSet != null)
                    Walk(Unwrap(fieldType), field.SelectionSet, op);
            }
        }

        foreach (var (name, text) in operations)
            Walk(root, Operation(text).SelectionSet, name);

        if (problems.Count > 0)
            throw new InvalidDataException("The live schema no longer supports these queries:\n  " +
                                           string.Join("\n  ", problems));

        var typesNode = new JsonObject();
        foreach (var (typeName, fields) in subset)
        {
            var fieldsNode = new JsonObject();
            foreach (var (fieldName, field) in fields) fieldsNode[fieldName] = field;
            typesNode[typeName] = fieldsNode;
        }

        return new JsonObject
        {
            ["source"] = LiveEndpoint.ToString(),
            ["queryType"] = root,
            ["types"] = typesNode
        };
    }

    public static GraphQLOperationDefinition Operation(string text)
    {
        var doc = Parser.Parse(text);
        return doc.Definitions.OfType<GraphQLOperationDefinition>().Single();
    }

    private static string TypeName(GraphQLType type)
    {
        return type switch
        {
            GraphQLNonNullType nn => TypeName(nn.Type) + "!",
            GraphQLListType list => "[" + TypeName(list.Type) + "]",
            GraphQLNamedType named => named.Name.StringValue,
            _ => "?"
        };
    }

    /// <summary>
    ///     What a GraphQL server would reject this query for against the contract: fields or arguments that are
    ///     not there, a required argument left out, or a variable whose type cannot be passed to the argument.
    /// </summary>
    public static List<string> Validate(JsonObject contract, string text)
    {
        var errors = new List<string>();
        GraphQLOperationDefinition op;
        try
        {
            op = Operation(text);
        }
        catch (Exception ex)
        {
            return new List<string> {$"does not parse: {ex.Message}"};
        }

        var variables = (op.Variables?.Items ?? new List<GraphQLVariableDefinition>())
            .ToDictionary(v => v.Variable.Name.StringValue, v => (Type: TypeName(v.Type), HasDefault: v.DefaultValue != null));
        var types = (JsonObject) contract["types"]!;

        void Walk(string typeName, GraphQLSelectionSet set)
        {
            if (types[typeName] is not JsonObject fields)
            {
                errors.Add($"type {typeName} is not in the contract");
                return;
            }

            foreach (var field in set.Selections.OfType<GraphQLField>())
            {
                var name = field.Name.StringValue;
                if (fields[name] is not JsonObject def)
                {
                    errors.Add($"{typeName}.{name} does not exist");
                    continue;
                }

                var args = (JsonObject) def["args"]!;
                var given = (field.Arguments?.Items ?? new List<GraphQLArgument>()).ToDictionary(a => a.Name.StringValue, a => a.Value);
                foreach (var (argName, value) in given)
                {
                    if (args[argName] is not JsonObject argDef)
                    {
                        errors.Add($"{typeName}.{name} has no argument {argName}");
                        continue;
                    }

                    var argType = argDef["type"]!.GetValue<string>();
                    switch (value)
                    {
                        case GraphQLVariable v:
                            if (!variables.TryGetValue(v.Name.StringValue, out var declared))
                                errors.Add($"${v.Name.StringValue} is not declared");
                            else if (!Assignable(declared.Type, declared.HasDefault, argType))
                                errors.Add($"${v.Name.StringValue} ({declared.Type}) cannot be passed to {typeName}.{name}({argName}: {argType})");
                            break;
                        default:
                            if (!LiteralFits(value, argType))
                                errors.Add($"{typeName}.{name}({argName}: {argType}) cannot take {value.Kind}");
                            break;
                    }
                }

                foreach (var (argName, argDef) in args)
                    if (argDef!["type"]!.GetValue<string>().EndsWith('!') && !argDef["hasDefault"]!.GetValue<bool>() &&
                        !given.ContainsKey(argName))
                        errors.Add($"{typeName}.{name} requires argument {argName}");

                var fieldType = def["type"]!.GetValue<string>();
                if (field.SelectionSet != null)
                    Walk(Unwrap(fieldType), field.SelectionSet);
            }
        }

        Walk(contract["queryType"]!.GetValue<string>(), op.SelectionSet);
        return errors;
    }

    private static bool Assignable(string variableType, bool hasDefault, string argumentType)
    {
        if (argumentType.EndsWith('!') && !variableType.EndsWith('!') && !hasDefault) return false;
        return variableType.TrimEnd('!') == argumentType.TrimEnd('!');
    }

    private static bool LiteralFits(GraphQLValue value, string type)
    {
        var named = type.TrimEnd('!');
        return value switch
        {
            GraphQLNullValue => !type.EndsWith('!'),
            GraphQLStringValue => named is "String" or "ID" || !IsBuiltIn(named),
            GraphQLIntValue => named is "Int" or "Float" or "ID",
            GraphQLFloatValue => named is "Float",
            GraphQLBooleanValue => named is "Boolean",
            GraphQLEnumValue => !IsBuiltIn(named),
            _ => true
        };
    }

    private static bool IsBuiltIn(string name)
    {
        return name is "String" or "Int" or "Float" or "Boolean" or "ID";
    }

    /// <summary>
    ///     Answers a query the way a GraphQL server does, from data a test supplies. <paramref name="resolve" />
    ///     is given each top-level field and its arguments (variables already substituted) and returns that
    ///     field's whole value; only the fields the query selected are passed back.
    /// </summary>
    public static JsonObject Execute(string text, JsonObject? variables,
        Func<string, IReadOnlyDictionary<string, JsonNode?>, JsonNode?> resolve)
    {
        var op = Operation(text);
        var data = new JsonObject();
        foreach (var field in op.SelectionSet.Selections.OfType<GraphQLField>())
        {
            var args = (field.Arguments?.Items ?? new List<GraphQLArgument>()).ToDictionary(
                a => a.Name.StringValue,
                a => a.Value is GraphQLVariable v ? variables?[v.Name.StringValue]?.DeepClone() : Literal(a.Value));
            var value = resolve(field.Name.StringValue, args);
            data[(field.Alias?.Name ?? field.Name).StringValue] = Project(value, field.SelectionSet);
        }

        return new JsonObject {["data"] = data};
    }

    private static JsonNode? Literal(GraphQLValue value)
    {
        return value switch
        {
            GraphQLStringValue s => JsonValue.Create(new string(s.Value.Span)),
            GraphQLIntValue i => JsonValue.Create(long.Parse(i.Value.Span)),
            GraphQLBooleanValue b => JsonValue.Create(b.BoolValue),
            GraphQLEnumValue e => JsonValue.Create(e.Name.StringValue),
            _ => null
        };
    }

    private static JsonNode? Project(JsonNode? value, GraphQLSelectionSet? set)
    {
        if (set == null || value == null) return value?.DeepClone();
        if (value is JsonArray array)
            return new JsonArray(array.Select(item => Project(item, set)).ToArray());
        var obj = (JsonObject) value;
        var result = new JsonObject();
        foreach (var field in set.Selections.OfType<GraphQLField>())
            result[(field.Alias?.Name ?? field.Name).StringValue] = Project(obj[field.Name.StringValue], field.SelectionSet);
        return result;
    }
}
