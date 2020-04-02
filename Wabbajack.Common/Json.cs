using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using File = Alphaleonis.Win32.Filesystem.File;

namespace Wabbajack.Common
{
    public static partial class Utils
    {
        public static List<JsonConverter> Converters = new List<JsonConverter>
        {
            new HashJsonConverter(),
            new RelativePathConverter(),
            new AbolutePathConverter(),
            new HashRelativePathConverter(),
            new FullPathConverter(),
            new GameConverter(),
            new PercentConverter(),
        };

        public static void ToJSON<T>(this T obj, string filename)
        {
            if (File.Exists(filename))
                File.Delete(filename);
            File.WriteAllText(filename,
                JsonConvert.SerializeObject(obj, Formatting.Indented,
                    new JsonSerializerSettings {TypeNameHandling = TypeNameHandling.Auto, Converters = Converters}));
        }

        public static string ToJSON<T>(this T obj,
            TypeNameHandling handling = TypeNameHandling.All,
            TypeNameAssemblyFormatHandling format = TypeNameAssemblyFormatHandling.Full,
            bool prettyPrint = false)
        {
            return JsonConvert.SerializeObject(obj, Formatting.Indented,
                new JsonSerializerSettings
                {
                    TypeNameHandling = handling,
                    TypeNameAssemblyFormatHandling = format,
                    Formatting = prettyPrint ? Formatting.Indented : Formatting.None,
                    Converters = Converters
                });
        }

        public static T FromJSON<T>(this AbsolutePath filename,
            TypeNameHandling handling = TypeNameHandling.All,
            TypeNameAssemblyFormatHandling format = TypeNameAssemblyFormatHandling.Full)
        {
            return JsonConvert.DeserializeObject<T>(filename.ReadAllText(),
                new JsonSerializerSettings
                {
                    TypeNameHandling = handling, TypeNameAssemblyFormatHandling = format, Converters = Converters
                })!;
        }

        public static T FromJSONString<T>(this string data,
            TypeNameHandling handling = TypeNameHandling.All,
            TypeNameAssemblyFormatHandling format = TypeNameAssemblyFormatHandling.Full)
        {
            return JsonConvert.DeserializeObject<T>(data,
                new JsonSerializerSettings
                {
                    TypeNameHandling = handling, TypeNameAssemblyFormatHandling = format, Converters = Converters
                })!;
        }

        public static T FromJSON<T>(this Stream data)
        {
            var s = Encoding.UTF8.GetString(data.ReadAll());
            try
            {
                return JsonConvert.DeserializeObject<T>(s,
                    new JsonSerializerSettings {TypeNameHandling = TypeNameHandling.Auto, Converters = Converters})!;
            }
            catch (JsonSerializationException)
            {
                var error = JsonConvert.DeserializeObject<NexusErrorResponse>(s,
                    new JsonSerializerSettings {TypeNameHandling = TypeNameHandling.Auto});
                if (error != null)
                    Log($"Exception while deserializing\nError code: {error.code}\nError message: {error.message}");
                throw;
            }
        }


        private class HashJsonConverter : JsonConverter<Hash>
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

        private class RelativePathConverter : JsonConverter<RelativePath>
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

        private class AbolutePathConverter : JsonConverter<AbsolutePath>
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

                GameMetaData? game = GameRegistry.GetByFuzzyName(str);
                if (game == null)
                {
                    throw new ArgumentException($"Could not convert {str} to a Game type.");
                }

                return game.Game;
            }
        }
    }
}
