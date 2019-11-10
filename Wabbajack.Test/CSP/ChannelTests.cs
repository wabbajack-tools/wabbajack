using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Wabbajack.Common.CSP;

namespace Wabbajack.Test.CSP
{
    [TestClass]
    public class ChannelTests
    {
        [TestMethod]
        public async Task PutThenTakeNoBuffer()
        {
            var chan = Channel.Create<int>();

            var putter = chan.Put(42);
            var taker = chan.Take();

            Assert.IsTrue(await putter);
            Assert.AreEqual((true, 42), await taker);
        }

        [TestMethod]
        public async Task TakeThenPushNoBuffer()
        {
            var chan = Channel.Create<int>();

            var taker = chan.Take();
            var putter = chan.Put(42);

            Assert.IsTrue(await putter);
            Assert.AreEqual((true, 42), await taker);
        }

        [TestMethod]
        public async Task TakeFromBufferAfterPut()
        {
            var chan = Channel.Create<int>(1);

            var putter = chan.Put(42);
            var taker = chan.Take();

            Assert.IsTrue(await putter);
            Assert.AreEqual((true, 42), await taker);
        }

        [TestMethod]
        public async Task TakeFromBufferBeforePut()
        {
            var chan = Channel.Create<int>(1);

            var taker = chan.Take();
            var putter = chan.Put(42);

            Assert.IsTrue(await putter);
            Assert.AreEqual((true, 42), await taker);
        }


        [TestMethod]
        public async Task TakesAreReleasedAfterClose()
        {
            var chan = Channel.Create<int>();

            var taker = chan.Take();
            chan.Close();

            Assert.AreEqual((false, 0), await taker);
        }

        [TestMethod]
        public async Task ExpandingTransformsReleaseMultipleTakes()
        {
            var chan = Channel.Create<int, int>(1, i => i.SelectMany(len => Enumerable.Range(0, len)));

            var take1 = chan.Take();
            var take2 = chan.Take();
            await chan.Put(2);
            
            Assert.AreEqual((true, 0), await take1);
            Assert.AreEqual((true, 1), await take2);
        }

        [TestMethod]
        public async Task TransformsCanCloseChannel()
        {
            var chan = Channel.Create<int, int>(1, i => i.Take(1));

            var take1 = chan.Take();
            var take2 = chan.Take();
            
            await chan.Put(1);
            await chan.Put(2);

            Assert.IsTrue(chan.IsClosed);

            Assert.AreEqual((true, 1), await take1);
            Assert.AreEqual((false, 0), await take2);
        }

        [TestMethod]
        public async Task TransformsCanCloseDuringExpand()
        {
            var chan = Channel.Create<int, int>(1, i => i.SelectMany(len => Enumerable.Range(1, len)).Take(1));

            var take1 = chan.Take();
            var take2 = chan.Take();

            await chan.Put(2);

            Assert.IsTrue(chan.IsClosed);

            Assert.AreEqual((true, 1), await take1);
            Assert.AreEqual((false, 0), await take2);
        }

        [TestMethod]
        public async Task TransformsCanFilterTakeFirst()
        {
            var chan = Channel.Create<int, int>(1, i => i.Where(x => x == 2));

            var take1 = chan.Take();
            var take2 = chan.Take();

            await chan.Put(1);
            await chan.Put(2);
            chan.Close();

            Assert.IsTrue(chan.IsClosed);

            Assert.AreEqual((true, 2), await take1);
            Assert.AreEqual((false, 0), await take2);
        }

        [TestMethod]
        public async Task TransformsCanReturnNothingTakeFirst()
        {
            var chan = Channel.Create<int, int>(1, i => i.Take(0));

            var take1 = chan.Take();
            var take2 = chan.Take();

            await chan.Put(1);

            Assert.IsTrue(chan.IsClosed);

            Assert.AreEqual((false, 0), await take1);
            Assert.AreEqual((false, 0), await take2);
        }

        [TestMethod]
        public async Task TransformsCanFilterTakeAfter()
        {
            var chan = Channel.Create<int, int>(1, i => i.Where(x => x == 2));


            await chan.Put(1);
            await chan.Put(2);

            var take1 = chan.Take();
            var take2 = chan.Take();
            chan.Close();

            Assert.IsTrue(chan.IsClosed);

            Assert.AreEqual((true, 2), await take1);
            Assert.AreEqual((false, 0), await take2);
        }

        [TestMethod]
        public async Task TransformsCanReturnNothingTakeAfter()
        {
            var chan = Channel.Create<int, int>(1, i => i.Take(0));

            await chan.Put(1);

            var take1 = chan.Take();
            var take2 = chan.Take();


            Assert.IsTrue(chan.IsClosed);

            Assert.AreEqual((false, 0), await take1);
            Assert.AreEqual((false, 0), await take2);
        }

        [TestMethod]
        public void TooManyTakesCausesException()
        {
            var chan = Channel.Create<int>();

            Assert.ThrowsException<ManyToManyChannel<int, int>.TooManyHanldersException>(() =>
            {
                for (var x = 0; x < ManyToManyChannel<int, int>.MAX_QUEUE_SIZE + 1; x++)
                    chan.Take();
            });
        }

        [TestMethod]
        public void TooManyPutsCausesException()
        {
            var chan = Channel.Create<int>();

            Assert.ThrowsException<ManyToManyChannel<int, int>.TooManyHanldersException>(() =>
            {
                for (var x = 0; x < ManyToManyChannel<int, int>.MAX_QUEUE_SIZE + 1; x++)
                    chan.Put(x);
            });
        }

        [TestMethod]
        public async Task BlockingPutsGoThroughTransform()
        {
            var chan = Channel.Create<int, int>(1, i => i.Take(2));

            var put1 = chan.Put(1);
            var put2 = chan.Put(2);
            var put3 = chan.Put(3);
            var put4 = chan.Put(4);

            var take1 = chan.Take();
            var take2 = chan.Take();
            var take3 = chan.Take();



            Assert.AreEqual((true, 1), await take1);
            Assert.AreEqual((true, 2), await take2);
            Assert.AreEqual((false, 0), await take3);

            Assert.IsTrue(await put1);
            Assert.IsTrue(await put2);
            Assert.IsFalse(await put3);
            Assert.IsFalse(await put4);
            Assert.IsTrue(chan.IsClosed);
        }
    }
}
