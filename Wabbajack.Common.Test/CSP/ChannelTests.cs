using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Wabbajack.Common.CSP;
using Xunit;

namespace Wabbajack.Common.CSP.Test
{
    public class ChannelTests
    {
        [Fact]
        public async Task PutThenTakeNoBuffer()
        {
            var chan = Channel.Create<int>();

            var putter = chan.Put(42);
            var taker = chan.Take();

            Assert.True(await putter);
            Assert.Equal((true, 42), await taker);
        }

        [Fact]
        public async Task TakeThenPushNoBuffer()
        {
            var chan = Channel.Create<int>();

            var taker = chan.Take();
            var putter = chan.Put(42);

            Assert.True(await putter);
            Assert.Equal((true, 42), await taker);
        }

        [Fact]
        public async Task TakeFromBufferAfterPut()
        {
            var chan = Channel.Create<int>(1);

            var putter = chan.Put(42);
            var taker = chan.Take();

            Assert.True(await putter);
            Assert.Equal((true, 42), await taker);
        }

        [Fact]
        public async Task TakeFromBufferBeforePut()
        {
            var chan = Channel.Create<int>(1);

            var taker = chan.Take();
            var putter = chan.Put(42);

            Assert.True(await putter);
            Assert.Equal((true, 42), await taker);
        }


        [Fact]
        public async Task TakesAreReleasedAfterClose()
        {
            var chan = Channel.Create<int>();

            var taker = chan.Take();
            chan.Close();

            Assert.Equal((false, 0), await taker);
        }

        [Fact]
        public async Task ExpandingTransformsReleaseMultipleTakes()
        {
            var chan = Channel.Create<int, int>(1, i => i.SelectMany(len => Enumerable.Range(0, len)));

            var take1 = chan.Take();
            var take2 = chan.Take();
            await chan.Put(2);
            
            Assert.Equal((true, 0), await take1);
            Assert.Equal((true, 1), await take2);
        }

        [Fact]
        public async Task TransformsCanCloseChannel()
        {
            var chan = Channel.Create<int, int>(1, i => i.Take(1));

            var take1 = chan.Take();
            var take2 = chan.Take();
            
            await chan.Put(1);
            await chan.Put(2);

            Assert.True(chan.IsClosed);

            Assert.Equal((true, 1), await take1);
            Assert.Equal((false, 0), await take2);
        }

        [Fact]
        public async Task TransformsCanCloseDuringExpand()
        {
            var chan = Channel.Create<int, int>(1, i => i.SelectMany(len => Enumerable.Range(1, len)).Take(1));

            var take1 = chan.Take();
            var take2 = chan.Take();

            await chan.Put(2);

            Assert.True(chan.IsClosed);

            Assert.Equal((true, 1), await take1);
            Assert.Equal((false, 0), await take2);
        }

        [Fact]
        public async Task TransformsCanFilterTakeFirst()
        {
            var chan = Channel.Create<int, int>(1, i => i.Where(x => x == 2));

            var take1 = chan.Take();
            var take2 = chan.Take();

            await chan.Put(1);
            await chan.Put(2);
            chan.Close();

            Assert.True(chan.IsClosed);

            Assert.Equal((true, 2), await take1);
            Assert.Equal((false, 0), await take2);
        }

        [Fact]
        public async Task TransformsCanReturnNothingTakeFirst()
        {
            var chan = Channel.Create<int, int>(1, i => i.Take(0));

            var take1 = chan.Take();
            var take2 = chan.Take();

            await chan.Put(1);

            Assert.True(chan.IsClosed);

            Assert.Equal((false, 0), await take1);
            Assert.Equal((false, 0), await take2);
        }

        [Fact]
        public async Task TransformsCanFilterTakeAfter()
        {
            var chan = Channel.Create<int, int>(1, i => i.Where(x => x == 2));


            await chan.Put(1);
            await chan.Put(2);

            var take1 = chan.Take();
            var take2 = chan.Take();
            chan.Close();

            Assert.True(chan.IsClosed);

            Assert.Equal((true, 2), await take1);
            Assert.Equal((false, 0), await take2);
        }

        [Fact]
        public async Task TransformsCanReturnNothingTakeAfter()
        {
            var chan = Channel.Create<int, int>(1, i => i.Take(0));

            await chan.Put(1);

            var take1 = chan.Take();
            var take2 = chan.Take();


            Assert.True(chan.IsClosed);

            Assert.Equal((false, 0), await take1);
            Assert.Equal((false, 0), await take2);
        }

        [Fact]
        public void TooManyTakesCausesException()
        {
            var chan = Channel.Create<int>();

            Assert.Throws<ManyToManyChannel<int, int>.TooManyHanldersException>(() =>
            {
                for (var x = 0; x < ManyToManyChannel<int, int>.MAX_QUEUE_SIZE + 1; x++)
                    chan.Take();
            });
        }

        [Fact]
        public void TooManyPutsCausesException()
        {
            var chan = Channel.Create<int>();

            Assert.Throws<ManyToManyChannel<int, int>.TooManyHanldersException>(() =>
            {
                for (var x = 0; x < ManyToManyChannel<int, int>.MAX_QUEUE_SIZE + 1; x++)
                    chan.Put(x);
            });
        }

        [Fact]
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



            Assert.Equal((true, 1), await take1);
            Assert.Equal((true, 2), await take2);
            Assert.Equal((false, 0), await take3);

            Assert.True(await put1);
            Assert.True(await put2);
            Assert.False(await put3);
            Assert.False(await put4);
            Assert.True(chan.IsClosed);
        }
    }
}
