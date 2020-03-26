using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;

namespace Wabbajack.Common
{
    public partial class Utils
    {
        private static MessagePackSerializerOptions _messagePackOptions;
        private static IFormatterResolver _resolver;

        private static void MessagePackInit()
        {
            _resolver = CompositeResolver.Create(
                new List<IMessagePackFormatter>
                {
                    new HashFormatter(),
                    new RelativePathFormatter(),
                    new AbsolutePathFormatter(),
                    new HashRelativePathFormatter(),
                    new FullPathFormatter()
                },
                new List<IFormatterResolver> {StandardResolver.Instance}
            );
            _messagePackOptions = MessagePackSerializerOptions.Standard
                .WithResolver(_resolver)
                .WithCompression(MessagePackCompression.Lz4BlockArray);

        }
        
        /// <summary>
        /// Writes a object to this stream using MessagePack
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="obj"></param>
        /// <typeparam name="T"></typeparam>
        public static async Task WriteAsMessagePackAsync<T>(this Stream stream, T obj)
        {
            await MessagePackSerializer.SerializeAsync(stream, obj, _messagePackOptions);
        }
        
        /// <summary>
        /// Writes a object to this stream using MessagePack
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="obj"></param>
        /// <typeparam name="T"></typeparam>
        public static void WriteAsMessagePack<T>(this Stream stream, T obj)
        {
            MessagePackSerializer.Serialize(stream, obj, _messagePackOptions);
        }

        /// <summary>
        /// Reads a object from this stream using MessagePack
        /// </summary>
        /// <param name="stream"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static async Task<T> ReadAsMessagePackAsync<T>(this Stream stream)
        {
            return await MessagePackSerializer.DeserializeAsync<T>(stream, _messagePackOptions);
        }

        
        /// <summary>
        /// Reads a object from this stream using MessagePack
        /// </summary>
        /// <param name="stream"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static T ReadAsMessagePack<T>(this Stream stream)
        {
            return MessagePackSerializer.Deserialize<T>(stream, _messagePackOptions);
        }

        
    }
    
    #region Formatters

    public class HashFormatter : IMessagePackFormatter<Hash>
    {
        public void Serialize(ref MessagePackWriter writer, Hash value, MessagePackSerializerOptions options)
        {
            writer.WriteUInt64((ulong)value);
        }

        public Hash Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            return new Hash(reader.ReadUInt64());
        }
    }
    
    public class RelativePathFormatter : IMessagePackFormatter<RelativePath>
    {
        public void Serialize(ref MessagePackWriter writer, RelativePath value, MessagePackSerializerOptions options)
        {
            var encoded = Encoding.UTF8.GetBytes((string)value);
            writer.WriteString(encoded);
        }

        public RelativePath Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            return (RelativePath)reader.ReadString();
        }
    }
    
    public class AbsolutePathFormatter : IMessagePackFormatter<AbsolutePath>
    {
        public void Serialize(ref MessagePackWriter writer, AbsolutePath value, MessagePackSerializerOptions options)
        {
            var encoded = Encoding.UTF8.GetBytes((string)value);
            writer.WriteString(encoded);
        }

        public AbsolutePath Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            return (AbsolutePath)reader.ReadString();
        }
    }
    
    public class HashRelativePathFormatter : IMessagePackFormatter<HashRelativePath>
    {
        public void Serialize(ref MessagePackWriter writer, HashRelativePath value, MessagePackSerializerOptions options)
        {
            writer.WriteArrayHeader(value.Paths.Length + 1);
            writer.WriteUInt64((ulong)value.BaseHash);
            foreach (var path in value.Paths)
            {
                var encoded = Encoding.UTF8.GetBytes((string)path);
                writer.WriteString(encoded);
            }
        }

        public HashRelativePath Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            var header = reader.ReadArrayHeader();
            var hash = Hash.FromULong(reader.ReadUInt64());
            var paths = new RelativePath[header - 1];
            for (int idx = 0; idx < header - 1; idx += 1)
            {
                paths[idx] = (RelativePath)reader.ReadString();
            }
            return new HashRelativePath(hash, paths);
        }
    }
    
    public class FullPathFormatter : IMessagePackFormatter<FullPath>
    {
        public void Serialize(ref MessagePackWriter writer, FullPath value, MessagePackSerializerOptions options)
        {
            writer.WriteArrayHeader(value.Paths.Length + 1);
            writer.WriteString(Encoding.UTF8.GetBytes((string)value.Base));
            foreach (var path in value.Paths)
            {
                var encoded = Encoding.UTF8.GetBytes((string)path);
                writer.WriteString(encoded);
            }
        }

        public FullPath Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            var header = reader.ReadArrayHeader();
            var basePath = (AbsolutePath)reader.ReadString(); 
            var paths = new RelativePath[header - 1];
            for (int idx = 0; idx < header - 1; idx += 1)
            {
                paths[idx] = (RelativePath)reader.ReadString();
            }
            return new FullPath(basePath, paths);
        }
    }
    
    #endregion
}
