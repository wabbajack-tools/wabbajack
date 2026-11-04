using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using Wabbajack.DTOs.BSA.ArchiveStates;
using Wabbajack.DTOs.BSA.FileStates;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.DTOs.Texture;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Xunit;

namespace Wabbajack.DTOs.Test.Compatibility;

/// <summary>
///     Reflection over the polymorphic families a modlist is made of, found the same way
///     Wabbajack.DTOs.ConverterGenerators finds them, so a type added to Wabbajack.DTOs is picked up here
///     without anyone having to remember these tests exist.
/// </summary>
public static class ModlistTypes
{
    /// <summary>
    ///     The base types that carry a <c>$type</c> discriminator in modlist JSON. Matches the list in
    ///     ConverterGenerators/Program.cs.
    /// </summary>
    public static readonly Type[] Families =
    {
        typeof(IDownloadState),
        typeof(Directive),
        typeof(IArchive),
        typeof(AFile)
    };

    public static Assembly DtoAssembly => typeof(Archive).Assembly;

    public static Type[] ConcreteTypes(Type family)
    {
        return DtoAssembly.GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface)
            .Where(t => t.IsAssignableTo(family))
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .ToArray();
    }

    public static Type FamilyOf(Type concrete)
    {
        return Families.Single(f => concrete.IsAssignableTo(f));
    }

    public static IEnumerable<(Type Family, Type Type)> AllConcreteTypes()
    {
        return Families.SelectMany(f => ConcreteTypes(f).Select(t => (f, t)));
    }

    public static string JsonName(Type type)
    {
        return type.GetCustomAttribute<JsonNameAttribute>(false)?.Name;
    }

    public static string[] JsonAliases(Type type)
    {
        return type.GetCustomAttributes<JsonAliasAttribute>(false).Select(a => a.Alias).ToArray();
    }

    /// <summary>
    ///     Properties Generated.cs is known not to write, so the round-trip tests leave them at their
    ///     defaults instead of failing on them, as "Type.Property". Empty: a type changed without
    ///     Generated.cs being regenerated (BA2State's Starfield fields were) shows up as a round-trip
    ///     failure, and the fix is to rerun Wabbajack.DTOs.ConverterGenerators, not to add an entry.
    /// </summary>
    public static readonly HashSet<string> KnownDroppedProperties = new();

    /// <summary>
    ///     The properties the generated converters read and write: public, readable, publicly settable
    ///     (init counts) and not marked <c>[JsonIgnore]</c>. Same filter as PolymorphicGenerator.
    /// </summary>
    public static PropertyInfo[] SerializedProperties(Type type)
    {
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .Where(p => p.SetMethod is {IsPublic: true})
            .Where(p => p.GetCustomAttribute<JsonIgnoreAttribute>() == null)
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .ToArray();
    }
}

/// <summary>
///     Builds an instance of a modlist type with every serialized property set to a value that is not
///     its default, recursing into nested objects and filling arrays of a polymorphic type with one of
///     each concrete type. A property type it has no rule for throws, naming the property, so a new
///     property cannot be skipped by the round-trip tests without someone noticing.
/// </summary>
public class SampleBuilder
{
    private int _counter;

    public object Build(Type type)
    {
        if (type.IsAbstract || type.IsInterface)
            type = ModlistTypes.ConcreteTypes(type).First();

        var instance = Activator.CreateInstance(type)!;
        foreach (var prop in ModlistTypes.SerializedProperties(type))
        {
            if (ModlistTypes.KnownDroppedProperties.Contains($"{type.Name}.{prop.Name}"))
                continue;

            var value = Value(prop.PropertyType, $"{type.Name}.{prop.Name}");
            prop.SetValue(instance, value);
            Assert.False(IsDefault(prop.GetValue(instance), prop.PropertyType),
                $"SampleBuilder left {type.Name}.{prop.Name} at its default value");
        }

        return instance;
    }

    private object Value(Type type, string name)
    {
        var n = ++_counter;

        if (type == typeof(string))
            return $"{name} value {n} with \"quotes\", a backslash \\ and \u00e9";
        if (type == typeof(bool))
            return true;
        if (type == typeof(byte) || type == typeof(sbyte) || type == typeof(short) || type == typeof(ushort) ||
            type == typeof(int) || type == typeof(uint) || type == typeof(long) || type == typeof(ulong))
            return Convert.ChangeType(n % 100 + 1, type);
        if (type.IsEnum)
        {
            var values = Enum.GetValues(type).Cast<object>().ToArray();
            return values.Last(v => !Equals(v, Activator.CreateInstance(type)));
        }

        if (type == typeof(Uri))
            return new Uri($"https://example.com/files/{n}/{name}.zip?query=a%20b");
        if (type == typeof(Version))
            return new Version(1, 2, 3, n);
        if (type == typeof(Hash))
            return Hash.FromULong((ulong) new Random(n).NextInt64(1, long.MaxValue) * 2 + 1);
        if (type == typeof(RelativePath))
            return (RelativePath) $"Folder {n}\\Sub Folder\\{name}.ext";
        if (type == typeof(AbsolutePath))
            return (AbsolutePath) $"C:\\Modlists\\Folder {n}\\{name}";
        if (type == typeof(HashRelativePath))
            return new HashRelativePath((Hash) Value(typeof(Hash), name),
                (RelativePath) Value(typeof(RelativePath), name),
                (RelativePath) Value(typeof(RelativePath), name));
        if (type == typeof(PHash))
            return new PHash(Enumerable.Range(n, PHash.SIZE).Select(i => (byte) i).ToArray());

        if (type.IsArray)
        {
            var element = type.GetElementType()!;
            var items = element.IsAbstract || element.IsInterface
                ? ModlistTypes.ConcreteTypes(element).Select(Build).ToArray()
                : new[] {Value(element, name), Value(element, name)};
            var array = Array.CreateInstance(element, items.Length);
            for (var i = 0; i < items.Length; i++) array.SetValue(items[i], i);
            return array;
        }

        if ((type.IsClass || type.IsInterface) && type.Namespace?.StartsWith("Wabbajack") == true)
            return Build(type);

        throw new NotSupportedException(
            $"SampleBuilder has no rule for {type} (property {name}). Add one so the property is covered.");
    }

    private static bool IsDefault(object value, Type type)
    {
        if (value == null) return true;
        if (value is Array a) return a.Length == 0;
        if (value is RelativePath r) return r.ToString() == "";
        if (value is PHash p) return p.Data == null;
        if (value is HashRelativePath h) return h.Parts == null;
        return type.IsValueType && value.Equals(Activator.CreateInstance(type));
    }
}

/// <summary>
///     Compares two object graphs property by property and reports the path of the first difference.
///     Paths, URIs and hash paths are compared by their text, which is stricter than their own Equals
///     (paths ignore case, <see cref="Uri" /> ignores fragments).
/// </summary>
public static class DeepCompare
{
    public static void AssertEqual(object expected, object actual, string path = "$")
    {
        if (expected == null || actual == null)
        {
            Assert.True(expected == null && actual == null,
                $"{path}: expected {Show(expected)} but got {Show(actual)}");
            return;
        }

        Assert.True(expected.GetType() == actual.GetType(),
            $"{path}: expected a {expected.GetType().Name} but got a {actual.GetType().Name}");

        switch (expected)
        {
            case string:
            case RelativePath:
            case AbsolutePath:
            case HashRelativePath:
            case Uri:
                Assert.True(expected.ToString() == actual.ToString(),
                    $"{path}: expected {Show(expected)} but got {Show(actual)}");
                return;
            case Array ea:
                var aa = (Array) actual;
                Assert.True(ea.Length == aa.Length, $"{path}: expected {ea.Length} items but got {aa.Length}");
                for (var i = 0; i < ea.Length; i++)
                    AssertEqual(ea.GetValue(i), aa.GetValue(i), $"{path}[{i}]");
                return;
        }

        var type = expected.GetType();
        if (type.IsPrimitive || type.IsEnum || expected is Hash || expected is Version || expected is decimal)
        {
            Assert.True(expected.Equals(actual), $"{path}: expected {Show(expected)} but got {Show(actual)}");
            return;
        }

        var props = ModlistTypes.SerializedProperties(type);
        if (props.Length == 0)
            props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                .ToArray();

        foreach (var prop in props)
            AssertEqual(prop.GetValue(expected), prop.GetValue(actual), $"{path}.{prop.Name}");

        if (expected is IDownloadState es)
            AssertEqual(es.PrimaryKeyString, ((IDownloadState) actual).PrimaryKeyString, $"{path}.PrimaryKeyString");
    }

    private static string Show(object o)
    {
        return o == null ? "null" : $"'{o}'";
    }
}
