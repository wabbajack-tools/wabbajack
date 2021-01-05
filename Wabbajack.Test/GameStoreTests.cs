using System.Threading.Tasks;
using Wabbajack.Common;
using Xunit;
using Xunit.Abstractions;

namespace Wabbajack.Test
{
    public class GameStoreTests : ATestBase
    {
        public GameStoreTests(ITestOutputHelper output) : base(output)
        {
        }

        
        /// <summary>
        ///  Comment out this [Fact] when not testing by hand. It's too hard to have all games installed at all times
        /// so we only run this test as neede
        /// </summary>
        /// <returns></returns>
        //[Fact]
        public async Task OriginGameStoreTest()
        {
            Assert.True(Game.DragonAgeOrigins.MetaData().TryGetGameLocation(out var loc));
            Assert.NotEqual(default, loc);
            Assert.Equal((AbsolutePath)@"c:\Games\Dragon Age", loc);
            Assert.True(Game.DragonAgeOrigins.MetaData().IsInstalled);
        }
    }
}
