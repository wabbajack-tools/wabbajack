using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Wabbajack.Common.CSP
{
    public static class Channel
    {
        /// <summary>
        /// Creates a channel without a buffer, and with no conversion function. This provides a syncronization
        /// point, where all puts are matched 1:1 with takes.
        /// </summary>
        /// <param name="bufferSize"></param>
        /// <typeparam name="T">The type of values transferred by the channel</typeparam>
        /// <returns>A new channel</returns>
        public static IChannel<T, T> Create<T>()
        {
            return new ManyToManyChannel<T, T>(x => x);
        }

        /// <summary>
        /// Creates a channel with a given enumerator as the starting buffer. Values will not be puttable into this channel
        /// and it will start closed. This is a easy way to spool a collection onto a channel. Note: the enumerator will be
        /// run inside the channel's lock, so it may not be wise to pass in an enumerator that performs heavy computation.
        /// </summary>
        /// <param name="e">A IEnumerator to use as the contents of the channel</param>
        /// <typeparam name="T">The type of values transferred by the channel</typeparam>
        /// <returns>A new channel</returns>
        public static IChannel<T, T> Create<T>(IEnumerator<T> e)
        {
            var chan = new ManyToManyChannel<T, T>(x => x, (_, __) => false, _ => {}, new EnumeratorBuffer<T>(e));
            chan.Close();
            return chan;
        }


        public static IChannel<T, T> Create<T>(int buffer_size)
        {
            var buffer = new FixedSizeBuffer<T>(buffer_size);
            return new ManyToManyChannel<T, T>(x => x, (buff, itm) =>
            {
                buff.Add(itm);
                return false;
            },
                b => {}, buffer);
        }

        public static IChannel<TIn, TOut> Create<TIn, TOut>(int buffer_size, Func<IObservable<TIn>, IObservable<TOut>> transform)
        {
            var buf = new RxBuffer<TIn, TOut>(buffer_size, transform);
            return ChannelForRxBuf(buf);
        }

        private static ManyToManyChannel<TIn, TOut> ChannelForRxBuf<TIn, TOut>(RxBuffer<TIn, TOut> buf)
        {
            return new ManyToManyChannel<TIn, TOut>(null, RxBuffer<TIn,TOut>.TransformAdd, RxBuffer<TIn, TOut>.Finalize, buf);
        }

        /// <summary>
        /// Creates a channel that discards every value
        /// </summary>
        /// <typeparam name="TIn"></typeparam>
        /// <returns></returns>
        public static IChannel<TIn, TIn> CreateSink<TIn>()
        {
            var buf = new RxBuffer<TIn, TIn>(1, e => e.Where(itm => false));
            return ChannelForRxBuf(buf);
        }
    }
}
