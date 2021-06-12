using System.Linq;
using Xunit;

namespace Wabbajack.Common.Test
{
    public class GameTests
    {
        [Fact]
        public void TestGetGameFromMO2ArchiveName()
        {
            var games = GameRegistry.Games.Select(x => (name: x.Value.MO2Name, game: x.Key));
            foreach (var (name, game) in games)
            {
                if (name == null) continue;
                var result = GameRegistry.TryGetByFuzzyName(name);
                Assert.NotNull(result);
                Assert.Equal(game, result.Game);
            }
        }

        [Fact]
        public void GamePathsDontIncludeDuplicateBackslash()
        {
            var path = Game.Morrowind.MetaData().GameLocation();
            Assert.DoesNotContain("\\\\", path.ToString());
        }
    }
}
