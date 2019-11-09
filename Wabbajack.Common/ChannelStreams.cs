using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Wabbajack.Common
{
    public static class ChannelStreams
    {
        public static async Task IntoChannel<T>(this IEnumerable<T> coll, Channel<T> dest, bool closeAtEnd = false)
        {
            foreach (var itm in coll)
            {
                await dest.Writer.WriteAsync(itm);
            }

            if (closeAtEnd)
                dest.Writer.Complete();
        }

        public static Channel<T> AsChannel<T>(this IEnumerable<T> coll)
        {
            var chan = Channel.CreateUnbounded<T>();
            coll.IntoChannel(chan, true);
            return chan;
        }

        public static async Task<IEnumerable<T>> ToIEnumerable<T>(this Channel<T> src)
        {
            var buffer = new List<T>();
            while (true)
            {
                var result = await src.Reader.ReadAsync();
                buffer.Add(result);
            }

            return buffer;
        }
    }
}
