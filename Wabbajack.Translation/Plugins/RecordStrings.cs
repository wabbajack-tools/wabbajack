using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.RegularExpressions;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Strings;

namespace Wabbajack.Translation.Plugins;

public static class RecordStrings
{
    private static readonly HashSet<string> RecordNamespaces = ["Mutagen.Bethesda.Fallout4", "Mutagen.Bethesda.Skyrim"];
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> GetterProperties = new();
    private static readonly Regex Segment = new(@"^(\w+)(?:\[(\d+)\])?$", RegexOptions.Compiled);

    public static bool IsSkippedRecord(IMajorRecordGetter record)
    {
        // Placed refs, landscape and navmesh hold no player-facing text and are big
        var name = record.GetType().Name;
        return name.Contains("Placed") || name.StartsWith("Landscape") || name.StartsWith("NavigationMesh");
    }

    public static string RecordTypeName(IMajorRecordGetter record) =>
        record.GetType().Name.Replace("BinaryOverlay", "");

    public static List<(string Path, string? Text)> Extract(object record)
    {
        var into = new List<(string, ITranslatedStringGetter)>();
        Extract(record, "", into, 0);
        return into.Select(x => (x.Item1, x.Item2.String)).ToList();
    }

    public static string FieldName(string recordType, string path) =>
        recordType + "." + Regex.Replace(path, @"\[\d+\]", "[]");

    private static PropertyInfo[] PropertiesOf(Type type) => GetterProperties.GetOrAdd(type, t =>
        t.GetInterfaces()
            .Where(i => i.Namespace != null && RecordNamespaces.Contains(i.Namespace) && i.Name.EndsWith("Getter"))
            .SelectMany(i => i.GetProperties())
            .Where(p => p.GetIndexParameters().Length == 0)
            .GroupBy(p => p.Name).Select(g => g.First())
            .Where(p => p.Name is not ("FormKey" or "EditorID" or "Registration" or "StaticRegistration"))
            .ToArray());

    private static void Extract(object obj, string prefix, List<(string, ITranslatedStringGetter)> into, int depth)
    {
        if (depth > 8) return;
        foreach (var property in PropertiesOf(obj.GetType()))
        {
            var type = property.PropertyType;
            if (typeof(ITranslatedStringGetter).IsAssignableFrom(type))
            {
                if (property.GetValue(obj) is ITranslatedStringGetter value)
                    into.Add((prefix + property.Name, value));
                continue;
            }

            if (type.IsGenericType && typeof(ITranslatedStringGetter).IsAssignableFrom(type.GetGenericArguments()[0]))
            {
                if (property.GetValue(obj) is IGenderedItemGetter<ITranslatedStringGetter?> gendered)
                {
                    if (gendered.Male != null) into.Add((prefix + property.Name + ".Male", gendered.Male));
                    if (gendered.Female != null) into.Add((prefix + property.Name + ".Female", gendered.Female));
                }

                continue;
            }

            if (type == typeof(string) || type.IsValueType || type.Name.StartsWith("IFormLink")) continue;
            if (type.IsGenericType && type.GetGenericArguments().Any(a => a.Name.StartsWith("IFormLink"))) continue;
            var ns = type.IsGenericType ? type.GetGenericArguments()[0].Namespace : type.Namespace;
            if (ns == null || !RecordNamespaces.Contains(ns)) continue;

            object? value2;
            try
            {
                value2 = property.GetValue(obj);
            }
            catch
            {
                continue;
            }

            if (value2 == null || value2 is IMajorRecordGetter) continue;
            if (value2 is IEnumerable items)
            {
                var index = 0;
                foreach (var item in items)
                {
                    if (item != null && item is not IMajorRecordGetter &&
                        item.GetType().Namespace?.StartsWith("Mutagen") == true)
                        Extract(item, $"{prefix}{property.Name}[{index}].", into, depth + 1);
                    index++;
                }
            }
            else
            {
                Extract(value2, prefix + property.Name + ".", into, depth + 1);
            }
        }
    }

    public static void Localize(object record, Language language)
    {
        var strings = new List<(string, ITranslatedStringGetter)>();
        Extract(record, "", strings, 0);
        foreach (var (path, value) in strings)
        {
            var text = value.TryLookup(language, out var found) ? found : value.String;
            if (text != null)
                Set(record, path, text, language);
        }
    }

    public static bool Set(object record, string path, string text, Language language = Language.English)
    {
        var segments = path.Split('.');
        object? current = record;
        for (var i = 0; i < segments.Length; i++)
        {
            var match = Segment.Match(segments[i]);
            if (!match.Success || current == null) return false;
            var property = current.GetType().GetProperties()
                .FirstOrDefault(p => p.Name == match.Groups[1].Value && p.GetIndexParameters().Length == 0);
            if (property == null) return false;

            if (i == segments.Length - 1)
            {
                if (!property.CanWrite) return false;
                property.SetValue(current, new TranslatedString(language, text));
                return true;
            }

            current = property.GetValue(current);
            if (match.Groups[2].Success)
            {
                if (current is not IEnumerable sequence) return false;
                current = sequence.Cast<object?>().ElementAtOrDefault(int.Parse(match.Groups[2].Value));
            }
        }

        return false;
    }
}
