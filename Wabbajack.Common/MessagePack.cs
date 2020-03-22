using System.Collections.Generic;
using System.IO;
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
                new List<IMessagePackFormatter>{new HashFormatter()},
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
    
    #endregion
}
