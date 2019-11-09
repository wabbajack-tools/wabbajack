using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Wabbajack.Common.CSP;

namespace Wabbajack.Test
{
    [TestClass]
    public class CSPTests
    {
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

        [TestMethod]
        public async Task TestTakeAll()
        {
            var results = await Enumerable.Range(0, 10).ToChannel().TakeAll();

            CollectionAssert.AreEqual(Enumerable.Range(0, 10).ToList(), results);
        }
    }
}
