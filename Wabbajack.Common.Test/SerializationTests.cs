using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Wabbajack.Common.Test
{
    public class SerializationTests
    {

        [Fact]
        public async Task HashRoundTrips()
        {
            await RoundTrips(Hash.FromULong(42));
            await RoundTrips(Hash.FromULong(ulong.MaxValue));
            await RoundTrips(Hash.FromULong(ulong.MinValue));
            await RoundTrips(Hash.FromLong(long.MaxValue));
            await RoundTrips(Hash.FromLong(long.MinValue));
        }

        [Fact]
        public async Task RelativePathsRoundTrips()
        {
            await RoundTrips((RelativePath)@"foo.txt");
            await RoundTrips((RelativePath)@"foo");
            await RoundTrips((RelativePath)@"\\far\\foo.txt");
            await RoundTrips((RelativePath)@"\\foo");
            await RoundTrips((RelativePath)@"\\baz");
        }

        [Fact]
        public async Task AbsolutePathRoundTrips()
        {
            await RoundTrips((AbsolutePath)@"c:\foo.txt");
            await RoundTrips((AbsolutePath)@"c:\foo");
            await RoundTrips((AbsolutePath)@"z:\far\foo.txt");
            await RoundTrips((AbsolutePath)@"r:\foo");
            await RoundTrips((AbsolutePath)@"f:\baz");
        }
        
        [Fact]
        public async Task HashRelativePathRoundTrips()
        {
            await RoundTrips(new HashRelativePath(Hash.FromULong(42), (RelativePath)"foo/bar.zip", (RelativePath)"baz.txt"));
            await RoundTrips(new HashRelativePath(Hash.FromULong(42)));
        }
        
        [Fact]
        public async Task FullPathRoundTrips()
        {
            await RoundTrips(new FullPath((AbsolutePath)@"c:\tmp", (RelativePath)"foo/bar.zip", (RelativePath)"baz.txt"));
            await RoundTrips(new FullPath((AbsolutePath)@"c:\"));
        }

        private static async Task RoundTrips<T>(T input)
        {
            Assert.Equal(input, RoundTripJson(input));
            Assert.Equal(input, await RoundTripMessagePack(input));
        }

        private static T RoundTripJson<T>(T input)
        {
            return input.ToJSON().FromJSONString<T>();
        }

        private static async Task<T> RoundTripMessagePack<T>(T input)
        {
            await using var ms = new MemoryStream();
            await ms.WriteAsMessagePackAsync(input);
            ms.Position = 0;
            return await ms.ReadAsMessagePackAsync<T>();
        }
    }
}
