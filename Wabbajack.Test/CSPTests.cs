using System;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Wabbajack.Common.CSP;

namespace Wabbajack.Test
{
    [TestClass]
    public class CSPTests
    {
        /// <summary>
        /// Test that we can put a value onto a channel without a buffer, and that the put is released once the
        /// take finalizes
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task TestTakePutBlocking()
        {
            var channel = Channel.Create<int>();
            var ptask = channel.Put(1);
            var (open, val) = await channel.Take();

            Assert.AreEqual(1, val);
            Assert.IsTrue(open);
            Assert.IsTrue(await ptask);
        }

        /// <summary>
        /// If we create a channel with a fixed buffer size, we can enqueue that number of items without blocking
        /// We can then take those items later on.
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task TestTakePutBuffered()
        {
            var channel = Channel.Create<int>(10);
            foreach (var itm in Enumerable.Range(0, 10))
                await channel.Put(itm);

            foreach (var itm in Enumerable.Range(0, 10))
            {
                var (is_open, val) = await channel.Take();
                Assert.AreEqual(itm, val);
                Assert.IsTrue(is_open);
            }
        }

        /// <summary>
        /// We can convert a IEnumerable into a channel by inlining the enumerable into the channel's buffer. 
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task TestToChannel()
        {
            var channel = Enumerable.Range(0, 10).ToChannel();

            foreach (var itm in Enumerable.Range(0, 10))
            {
                var (is_open, val) = await channel.Take();
                Assert.AreEqual(itm, val);
                Assert.IsTrue(is_open);
            }
        }


        /// <summary>
        /// TakeAll will continue to take from a channel as long as the channel is open. Once the channel closes
        /// TakeAll returns a list of the items taken.
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task TestTakeAll()
        {
            var results = await Enumerable.Range(0, 10).ToChannel().TakeAll();

            CollectionAssert.AreEqual(Enumerable.Range(0, 10).ToList(), results);
        }

        /// <summary>
        /// We can add Rx transforms as transforms inside a channel. This allows for cheap conversion and calcuation
        /// to be performed in a channel without incuring the dispatch overhead of swapping values between threads.
        /// These calculations happen inside the channel's lock, however, so be sure to keep these operations relatively
        /// cheap.
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task RxTransformInChannel()
        {
            var chan = Channel.Create<int, int>(1, o => o.Select(v => v + 1));
            var finished = Enumerable.Range(0, 10).OntoChannel(chan);

            foreach (var itm in Enumerable.Range(0, 10))
            {
                var (is_open, val) = await chan.Take();
                Assert.AreEqual(itm + 1, val);
                Assert.IsTrue(is_open);
            }
            await finished;
        }

        [TestMethod]
        public async Task UnorderedPipeline()
        {
            var o = Channel.Create<string>(3);
            var finished = Enumerable.Range(0, 3)
                .ToChannel()
                .UnorderedPipeline(1, o, obs => obs.Select(itm => itm.ToString()));

            var results = (await o.TakeAll()).OrderBy(e => e).ToList();
            var expected = Enumerable.Range(0, 3).Select(i => i.ToString()).ToList();
            CollectionAssert.AreEqual(expected, results);

        }
    }
}
