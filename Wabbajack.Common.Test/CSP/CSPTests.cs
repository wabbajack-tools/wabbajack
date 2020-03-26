using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Common.CSP;
using Xunit;

namespace Wabbajack.Common.CSP.Test
{
    public class CSPTests
    {
        /// <summary>
        /// Test that we can put a value onto a channel without a buffer, and that the put is released once the
        /// take finalizes
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task TestTakePutBlocking()
        {
            var channel = Channel.Create<int>();
            var ptask = channel.Put(1);
            var (open, val) = await channel.Take();

            Assert.Equal(1, val);
            Assert.True(open);
            Assert.True(await ptask);
        }

        /// <summary>
        /// If we create a channel with a fixed buffer size, we can enqueue that number of items without blocking
        /// We can then take those items later on.
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task TestTakePutBuffered()
        {
            var channel = Channel.Create<int>(10);
            foreach (var itm in Enumerable.Range(0, 10))
                await channel.Put(itm);

            foreach (var itm in Enumerable.Range(0, 10))
            {
                var (is_open, val) = await channel.Take();
                Assert.Equal(itm, val);
                Assert.True(is_open);
            }
        }

        /// <summary>
        /// We can convert a IEnumerable into a channel by inlining the enumerable into the channel's buffer. 
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task TestToChannel()
        {
            var channel = Enumerable.Range(0, 10).ToChannel();

            foreach (var itm in Enumerable.Range(0, 10))
            {
                var (is_open, val) = await channel.Take();
                Assert.Equal(itm, val);
                Assert.True(is_open);
            }
        }


        /// <summary>
        /// TakeAll will continue to take from a channel as long as the channel is open. Once the channel closes
        /// TakeAll returns a list of the items taken.
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task TestTakeAll()
        {
            var results = await Enumerable.Range(0, 10).ToChannel().TakeAll();

            Assert.Equal(Enumerable.Range(0, 10).ToList(), results);
        }

        /// <summary>
        /// We can add Rx transforms as transforms inside a channel. This allows for cheap conversion and calcuation
        /// to be performed in a channel without incuring the dispatch overhead of swapping values between threads.
        /// These calculations happen inside the channel's lock, however, so be sure to keep these operations relatively
        /// cheap.
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task RxTransformInChannel()
        {
            var chan = Channel.Create<int, int>(1, o => o.Select(v => v + 1));
            var finished = Enumerable.Range(0, 10).OntoChannel(chan);

            foreach (var itm in Enumerable.Range(0, 10))
            {
                var (is_open, val) = await chan.Take();
                Assert.Equal(itm + 1, val);
                Assert.True(is_open);
            }
            await finished;
        }

        [Fact]
        public async Task UnorderedPipeline()
        {
            var o = Channel.Create<string>(3);
            var finished = Enumerable.Range(0, 3)
                .ToChannel()
                .UnorderedPipeline(1, o, obs => obs.Select(itm => itm.ToString()));

            var results = (await o.TakeAll()).OrderBy(e => e).ToList();
            var expected = Enumerable.Range(0, 3).Select(i => i.ToString()).OrderBy(e => e).ToList();
            Assert.Equal(expected, results);

        }

        [Fact]
        public async Task UnorderedPipelineWithParallelism()
        {
            // Do it a hundred times to try and catch rare deadlocks
            var o = Channel.Create<string>(3);
            var finished = Enumerable.Range(0, 1024)
                .ToChannel()
                .UnorderedPipeline(4, o, obs => obs.Select(itm => itm.ToString()));

            var results = (await o.TakeAll()).OrderBy(e => e).ToList();
            var expected = Enumerable.Range(0, 1024).Select(i => i.ToString()).OrderBy(e => e).ToList();
            Assert.Equal(expected, results);
            await finished;
        }

        [Fact]
        public async Task UnorderedTaskPipeline()
        {
            // Do it a hundred times to try and catch rare deadlocks
            var o = Channel.Create<int>(3);
            var finished = Enumerable.Range(0, 1024)
                .ToChannel()
                .UnorderedPipeline(4, o, async v =>
                {
                    await Task.Delay(1);
                    return v;
                });

            var results = (await o.TakeAll()).OrderBy(e => e).ToList();
            var expected = Enumerable.Range(0, 1024).ToList();
            Assert.Equal(expected, results);
            await finished;
        }

        [Fact]
        public async Task UnorderedThreadPipeline()
        {
            // Do it a hundred times to try and catch rare deadlocks
            var o = Channel.Create<int>(3);
            var finished = Enumerable.Range(0, 1024)
                .ToChannel()
                .UnorderedThreadedPipeline(4, o, v =>
                {
                    Thread.Sleep(1);
                    return v;
                });

            var results = (await o.TakeAll()).OrderBy(e => e).ToList();
            var expected = Enumerable.Range(0, 1024).ToList();
            Assert.Equal(expected, results);
            await finished;
        }

        [Fact]
        public async Task ChannelStressTest()
        {
            var chan = Channel.Create<int>();

            var putter = Task.Run(async () =>
            {
                for (var i = 0; i < 1000; i++)
                {
                    var result = await chan.Put(i);
                }
            });

            var taker = Task.Run(async () =>
            {
                try
                {
                    for (var i = 0; i < 1000; i++)
                    {
                        var (is_open, val) = await chan.Take();
                        Assert.Equal(i, val);
                    }
                }
                finally
                {
                    chan.Close();
                }
            });

            await putter;
            await taker;
        }

        [Fact]
        public async Task ChannelStressWithBuffer()
        {
            var chan = Channel.Create<int>(1);

            var putter = Task.Run(async () =>
            {
                for (var i = 0; i < 1000; i++)
                {
                    await chan.Put(i);
                }
            });

            var taker = Task.Run(async () =>
            {
                try
                {
                    for (var i = 0; i < 1000; i++)
                    {
                        var (is_open, val) = await chan.Take();
                        Assert.Equal(i, val);
                    }
                }
                finally
                {
                    chan.Close();
                }
            });

            await putter;
            await taker;
        }
    }
}
