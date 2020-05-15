using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Wabbajack.Common.Serialization.Json;
using File = Alphaleonis.Win32.Filesystem.File;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace Wabbajack.Common
{
    public static partial class Utils
    {
        public static List<JsonConverter> Converters = new List<JsonConverter>
        {
            new HashJsonConverter(),
            new RelativePathConverter(),
            new AbsolutePathConverter(),
            new HashRelativePathConverter(),
            new FullPathConverter(),
            new GameConverter(),
            new PercentConverter(),
            new IPathConverter(),
        };
        
        public static JsonSerializerSettings JsonSettings  =>
            new JsonSerializerSettings {
                TypeNameHandling = TypeNameHandling.Objects,
                SerializationBinder = new JsonNameSerializationBinder(),
                Converters = Converters,
                DateTimeZoneHandling = DateTimeZoneHandling.Utc
            };

        public static JsonSerializerSettings GenericJsonSettings =>
            new JsonSerializerSettings
            {
                DateTimeZoneHandling = DateTimeZoneHandling.Utc,
            };


        public static void ToJson<T>(this T obj, string filename)
        {
            if (File.Exists(filename))
                File.Delete(filename);
            File.WriteAllText(filename, JsonConvert.SerializeObject(obj, Formatting.Indented, JsonSettings));
        }
        
        public static void ToJson<T>(this T obj, Stream stream)
        {
            using var tw = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true);
            using var writer = new JsonTextWriter(tw);
            var ser = JsonSerializer.Create(JsonSettings);
            ser.Serialize(writer, obj);
        }
        
        public static void ToJson<T>(this T obj, AbsolutePath path)
        {
            using var fs = path.Create();
            obj.ToJson(fs);
        }

        public static string ToJson<T>(this T obj)
        {
            return JsonConvert.SerializeObject(obj, JsonSettings);
        }

        public static T FromJson<T>(this AbsolutePath filename)
        {
            return JsonConvert.DeserializeObject<T>(filename.ReadAllText(), JsonSettings)!;
        }

        public static T FromJsonString<T>(this string data)
        {
            return JsonConvert.DeserializeObject<T>(data, JsonSettings)!;
        }

        public static T FromJson<T>(this Stream stream, bool genericReader = false)
        {
            using var tr = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
            using var reader = new JsonTextReader(tr);
            var ser = JsonSerializer.Create(genericReader ? GenericJsonSettings : JsonSettings);
            var result = ser.Deserialize<T>(reader);
            if (result == null)
                throw new JsonException("Type deserialized into null");
            return result;
        }

        public class HashJsonConverter : JsonConverter<Hash>
        {
            public override void WriteJson(JsonWriter writer, Hash value, JsonSerializer serializer)
            {
                writer.WriteValue(value.ToBase64());
            }

            public override Hash ReadJson(JsonReader reader, Type objectType, Hash existingValue, bool hasExistingValue,
                JsonSerializer serializer)
            {
                return Hash.FromBase64((string)reader.Value!);
            }
        }

        public class RelativePathConverter : JsonConverter<RelativePath>
        {
            public override void WriteJson(JsonWriter writer, RelativePath value, JsonSerializer serializer)
            {
                writer.WriteValue((string)value);
            }

            public override RelativePath ReadJson(JsonReader reader, Type objectType, RelativePath existingValue,
                bool hasExistingValue,
                JsonSerializer serializer)
            {
                return (RelativePath)(string)reader.Value!;
            }
        }

        private class AbsolutePathConverter : JsonConverter<AbsolutePath>
        {
            public override void WriteJson(JsonWriter writer, AbsolutePath value, JsonSerializer serializer)
            {
                writer.WriteValue((string)value);
            }

            public override AbsolutePath ReadJson(JsonReader reader, Type objectType, AbsolutePath existingValue,
                bool hasExistingValue,
                JsonSerializer serializer)
            {
                return (AbsolutePath)(string)reader.Value!;
            }
        }

        private class PercentConverter : JsonConverter<Percent>
        {
            public override Percent ReadJson(JsonReader reader, Type objectType, Percent existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                double d = (double)reader.Value!;
                return Percent.FactoryPutInRange(d);
            }

            public override void WriteJson(JsonWriter writer, Percent value, JsonSerializer serializer)
            {
                writer.WriteValue(value.Value);
            }
        }

        private class HashRelativePathConverter : JsonConverter<HashRelativePath>
        {
            public override void WriteJson(JsonWriter writer, HashRelativePath value, JsonSerializer serializer)
            {
                writer.WriteStartArray();
                writer.WriteValue(value.BaseHash.ToBase64());
                foreach (var itm in value.Paths)
                    writer.WriteValue((string)itm);
                writer.WriteEndArray();
            }

            public override HashRelativePath ReadJson(JsonReader reader, Type objectType,
                HashRelativePath existingValue, bool hasExistingValue,
                JsonSerializer serializer)
            {
                if (reader.TokenType != JsonToken.StartArray)
                    throw new JsonException("Invalid JSON state while reading Hash Relative Path");
                reader.Read();

                var hash = Hash.FromBase64((string)reader.Value!);
                var paths = new List<RelativePath>();

                reader.Read();
                while (reader.TokenType != JsonToken.EndArray)
                {
                    paths.Add((RelativePath)(string)reader.Value!);
                    reader.Read();
                }

                return new HashRelativePath(hash, paths.ToArray());
            }
        }

        private class FullPathConverter : JsonConverter<FullPath>
        {
            public override void WriteJson(JsonWriter writer, FullPath value, JsonSerializer serializer)
            {
                writer.WriteStartArray();
                writer.WriteValue((string)value.Base);
                foreach (var itm in value.Paths)
                    writer.WriteValue((string)itm);
                writer.WriteEndArray();
            }

            public override FullPath ReadJson(JsonReader reader, Type objectType, FullPath existingValue,
                bool hasExistingValue,
                JsonSerializer serializer)
            {
                if (reader.TokenType != JsonToken.StartArray)
                    throw new JsonException("Invalid JSON state while reading Hash Relative Path");
                reader.Read();

                var abs = (AbsolutePath)(string)reader.Value!;
                var paths = new List<RelativePath>();

                reader.Read();
                while (reader.TokenType != JsonToken.EndArray)
                {
                    paths.Add((RelativePath)(string)reader.Value!);
                    reader.Read();
                }

                return new FullPath(abs, paths.ToArray());
            }
        }

        public class GameConverter : JsonConverter<Game>
        {
            public override void WriteJson(JsonWriter writer, Game value, JsonSerializer serializer)
            {
                writer.WriteValue(Enum.GetName(typeof(Game), value));
            }

            public override Game ReadJson(JsonReader reader, Type objectType, Game existingValue,
                bool hasExistingValue,
                JsonSerializer serializer)
            {
                // Backwards compatibility support
                var str = reader.Value?.ToString();
                if (string.IsNullOrWhiteSpace(str)) return default;
                if (int.TryParse(str, out var i))
                {
                    return (Game)i;
                }

                if (!GameRegistry.TryGetByFuzzyName(str, out var game))
                {
                    throw new ArgumentException($"Could not convert {str} to a Game type.");
                }

                return game.Game;
            }
        }
        
        public class IPathConverter : JsonConverter<IPath>
        {
            public override void WriteJson(JsonWriter writer, IPath value, JsonSerializer serializer)
            {
                writer.WriteValue(Enum.GetName(typeof(Game), value));
            }

            public override IPath ReadJson(JsonReader reader, Type objectType, IPath existingValue,
                bool hasExistingValue,
                JsonSerializer serializer)
            {
                // Backwards compatibility support
                var str = reader.Value?.ToString();
                if (string.IsNullOrWhiteSpace(str)) return default(RelativePath);

                if (Path.IsPathRooted(str))
                    return (AbsolutePath)str;
                return (RelativePath)str;

            }
        }


        
        public class JsonNameSerializationBinder : DefaultSerializationBinder
        {
            private static ConcurrentDictionary<string, Type> _nameToType = new ConcurrentDictionary<string, Type>();
            private static ConcurrentDictionary<Type, string> _typeToName = new ConcurrentDictionary<Type, string>();
            private static bool _init;

            public JsonNameSerializationBinder()
            {
                if (_init)
                    return;
                
                var customDisplayNameTypes =
                    AppDomain.CurrentDomain
                        .GetAssemblies()
                        .Where(a => a.FullName != null && !a.FullName.StartsWith("System") && !a.FullName.StartsWith("Microsoft"))
                        .SelectMany(a =>
                        {
                            try
                            {
                                return a.GetTypes();
                            }
                            catch (ReflectionTypeLoadException)
                            {
                                return new Type[0];
                            }
                        })
                        //concat with references if desired
                        .Where(x => x
                            .GetCustomAttributes(false)
                            .Any(y => y is JsonNameAttribute));

                foreach (var type in customDisplayNameTypes)
                {
                    var strName = type.GetCustomAttributes(false).OfType<JsonNameAttribute>().First().Name;
                    _typeToName.TryAdd(type, strName);
                    _nameToType.TryAdd(strName, type);
                }
                _init = true;
            }

            public override Type BindToType(string? assemblyName, string typeName)
            {
                if (typeName.EndsWith("[]"))
                {
                    var result = BindToType(assemblyName, typeName.Substring(0, typeName.Length - 2));
                    return result.MakeArrayType();
                }

                if (_nameToType.ContainsKey(typeName))
                    return _nameToType[typeName];

                if (assemblyName != null)
                {
                    var assembly = Assembly.Load(assemblyName);
                    var tp = assembly
                        .GetTypes()
                        .FirstOrDefault(tp => Enumerable.OfType<JsonNameAttribute>(tp.GetCustomAttributes(false))
                            .Any(a => a.Name == typeName));

                    if (tp != null)
                    {
                        _typeToName.TryAdd(tp, typeName);
                        _nameToType.TryAdd(typeName, tp);
                        return tp;
                    }
                }

                var val = Type.GetType(typeName);
                if (val != null)
                    return val;

                return base.BindToType(assemblyName, typeName);
            }

            public override void BindToName(Type serializedType, out string? assemblyName, out string? typeName)
            {
                if (serializedType.FullName?.StartsWith("System.") ?? false)
                {
                    base.BindToName(serializedType, out assemblyName, out typeName);
                    return;
                }

                if (!_typeToName.ContainsKey(serializedType))
                {
                    var custom = serializedType.GetCustomAttributes(false)
                        .OfType<JsonNameAttribute>().FirstOrDefault();
                    if (custom == null)
                    {
                        throw new InvalidDataException($"No Binding name for {serializedType}");
                    }

                    _nameToType.TryAdd(custom.Name, serializedType);
                    _typeToName.TryAdd(serializedType, custom.Name);
                    assemblyName = serializedType.Assembly.FullName;
                    typeName = custom.Name;
                    return;
                }

                var name = _typeToName[serializedType];

                assemblyName = serializedType.Assembly.FullName;
                typeName = name;
            }
        }
    }
}
