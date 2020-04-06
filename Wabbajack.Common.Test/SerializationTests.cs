using System.IO;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Newtonsoft.Json.Converters;
using Wabbajack.Common.Serialization.Json;
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


        class Base
        {
            public int BaseNumber { get; set; }
        }

        [JsonName("ChildA")]
        class ChildA : Base
        {
            public int ChildANumber { get; set; }
        }

        [JsonName("ChildB")]
        class ChildB : ChildA
        {
            public int ChildBNumber { get; set; }
        }
        
        
        [Fact]
        public async Task JsonSerializationUser()
        {
            var start = new ChildB {BaseNumber = 1, ChildANumber = 2, ChildBNumber = 3};

            var result = (ChildB)start.ToJson().FromJsonString<Base>();

            Utils.Log(start.ToJson());
            
            Assert.Equal(start.BaseNumber, result.BaseNumber);
            Assert.Equal(start.ChildANumber, result.ChildANumber);
            Assert.Equal(start.ChildBNumber, result.ChildBNumber);

            Assert.DoesNotContain("Wabbajack.Common.Test.Serialization", start.ToJson());


        }

        private static async Task RoundTrips<T>(T input)
        {
            Assert.Equal(input, RoundTripJson(input));
        }

        private static T RoundTripJson<T>(T input)
        {
            return input.ToJson().FromJsonString<T>();
        }

    }
}
