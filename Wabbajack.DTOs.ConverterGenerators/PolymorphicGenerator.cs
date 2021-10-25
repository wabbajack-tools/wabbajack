using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wabbajack.DTOs.JsonConverters;

namespace Wabbajack.DTOs.ConverterGenerators;

public class PolymorphicGenerator<T>
{
    public PolymorphicGenerator()
    {
        var types = typeof(T).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface)
            .Where(t => t.IsAssignableTo(typeof(T)));

        foreach (var type in types)
        {
            var nameAttr = type.CustomAttributes.Where(t => t.AttributeType == typeof(JsonNameAttribute))
                .Select(t => (string) t.ConstructorArguments.First().Value!)
                .FirstOrDefault();

            if (nameAttr == default)
                throw new JsonException($"Type {type} of interface {typeof(T)} does not have a JsonNameAttribute");
            Registry[nameAttr] = type;
            ReverseRegistry[type] = nameAttr;

            var aliases = type.CustomAttributes.Where(t => t.AttributeType == typeof(JsonAliasAttribute))
                .Select(t => t.ConstructorArguments.First());

            foreach (var alias in aliases) Registry[(string) alias.Value!] = type;
        }
    }

    public Dictionary<string, Type> Registry { get; } = new();
    public Dictionary<Type, string> ReverseRegistry { get; } = new();

    public void GenerateSpecific(CFile c)
    {
        foreach (var type in ReverseRegistry.Keys.OrderBy(k => k.FullName))
        {
            var members = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead)
                .Where(p => p.CanWrite)
                .Where(p => !p.CustomAttributes.Any(c => c.AttributeType == typeof(JsonIgnoreAttribute)))
                .Select(p =>
                {
                    var name = p.CustomAttributes.Where(c => c.AttributeType == typeof(JsonPropertyNameAttribute))
                        .Select(a => (string) a.ConstructorArguments.FirstOrDefault().Value)
                        .FirstOrDefault() ?? p.Name;

                    return new
                    {
                        Name = name, PropName = name.ToLower() + "Prop", Property = p, Type = p.PropertyType,
                        RealName = p.Name
                    };
                })
                .OrderBy(p => p.Name)
                .ToArray();

            var mungedName = type.FullName!.Replace(".", "_");
            c.Code($"public class {mungedName}Converter : JsonConverter<{type.FullName}> {{");
            c.Code(
                $"public override {type.FullName} Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {{");
            c.Code("if (reader.TokenType != JsonTokenType.StartObject)");
            c.Code("  throw new JsonException();");
            foreach (var member in members) c.Code($"{member.Type.FullName} {member.PropName} = default;");

            c.Code("while (true) {");

            c.Code("reader.Read();");
            c.Code("if (reader.TokenType == JsonTokenType.EndObject) {");
            c.Code("reader.Read();");
            c.Code("break;");
            c.Code("}");
            c.Code("var prop = reader.GetString();");
            c.Code("reader.Read();");
            c.Code("switch (prop) {");

            foreach (var member in members)
            {
                c.Code($"case \"{member.Name}\":");
                c.Code(
                    $"  {member.PropName} = JsonSerializer.Deserialize<{member.Type.FullName}>(ref reader, options);");
                c.Code("  break;");
            }

            c.Code("default:");
            c.Code("  reader.Skip();");
            c.Code("  break;");

            c.Code("}");

            c.Code("}");

            c.Code($"return new {type.FullName} {{");

            foreach (var member in members) c.Code($"{member.RealName} = {member.PropName},");


            c.Code("};");

            c.Code("}");

            c.Code(
                $"public override void Write(Utf8JsonWriter writer, {type.FullName} value, JsonSerializerOptions options) {{");

            c.Code("writer.WriteStartObject();");
            c.Code($"writer.WriteString(\"$type\", \"{ReverseRegistry[type]}\");");

            foreach (var member in members)
            {
                c.Code($"writer.WritePropertyName(\"{member.Name}\");");
                c.Code(
                    $"JsonSerializer.Serialize<{member.Type.FullName}>(writer, value.{member.RealName}, options);");
            }

            c.Code("writer.WriteEndObject();");

            c.Code("}");

            c.Code("}");
        }
    }

    public void GenerateGeneric(CFile c)
    {
        var type = typeof(T);
        var mungedName = typeof(T).FullName!.Replace(".", "_");

        c.Code($"public class {mungedName}Converter : JsonConverter<{type.FullName}> {{");

        c.Code("public static void ConfigureServices(IServiceCollection services) {");

        foreach (var tp in ReverseRegistry.Keys)
            c.Code($"services.AddSingleton<JsonConverter, {tp.FullName!.Replace(".", "_")}Converter>();");
        c.Code($"services.AddSingleton<JsonConverter, {mungedName}Converter>();");

        c.Code("}");

        c.Code(
            $"public override {type.FullName} Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {{");

        c.Code("var cReader = reader;");
        c.Code("if (reader.TokenType != JsonTokenType.StartObject)");
        c.Code("  throw new JsonException();");
        c.Code("cReader.Read();");

        c.Code("if (cReader.GetString() != \"$type\")");
        c.Code("  throw new JsonException();");
        c.Code("cReader.Read();");
        c.Code("var type = cReader.GetString();");
        c.Code("switch(type) {");
        foreach (var (alias, tp) in Registry)
        {
            c.Code($"case \"{alias}\":");
            c.Code($"  return JsonSerializer.Deserialize<{tp.FullName}>(ref reader, options)!;");
        }

        c.Code("default:");
        c.Code("  throw new JsonException($\"No Type dispatch for {type}\");");

        c.Code("}");
        c.Code("}");

        c.Code(
            $"public override void Write(Utf8JsonWriter writer, {type.FullName} value, JsonSerializerOptions options) {{");

        c.Code("switch (value) {");
        var idx = 0;

        int Distance(Type t)
        {
            var depth = 0;
            var b = t;
            while (b != null)
            {
                b = b.BaseType;
                depth += 1;
            }

            return depth;
        }

        foreach (var t in ReverseRegistry.Keys.OrderByDescending(t => Distance(t)))
        {
            c.Code($"case {t.FullName} v{idx}:");
            c.Code($"  JsonSerializer.Serialize(writer, v{idx}, options);");
            c.Code("   return;");
            idx += 1;
        }

        c.Code("}");

        c.Code("}");

        c.Code("}");
    }

    public void GenerateAll(CFile cfile)
    {
        GenerateGeneric(cfile);
        GenerateSpecific(cfile);
    }
}