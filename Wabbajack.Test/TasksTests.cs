using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Lib.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Wabbajack.Test
{
    public class TasksTests : ACompilerTest
    {
        [Fact]
        public async Task CanRemapGameFolder()
        {
            await using var tempFolder = await TempFolder.Create();
            var gameff = tempFolder.Dir.Combine(Consts.GameFolderFilesDir);
            gameff.CreateDirectory();

            await gameff.Combine("some_file.txt").WriteAllTextAsync("some_file");
            await gameff.Combine("steam_api64.dll").WriteAllTextAsync("steam_api");
            

            var meta = Game.SkyrimSpecialEdition.MetaData(); 
            await tempFolder.Dir.Combine(Consts.ModOrganizer2Ini)
                .WriteAllLinesAsync(
                    "[General]",
                    $"gameName={meta.MO2Name}",
                    $"gamePath={meta.GameLocation()}",
                    $"pathDouble={meta.GameLocation().ToString().Replace(@"\", @"\\")}",
                    $"pathForward={meta.GameLocation().ToString().Replace(@"\", @"/")}");

            Assert.True(await MigrateGameFolder.Execute(tempFolder.Dir));
            
            Assert.Equal("some_file", await gameff.Combine("some_file.txt").ReadAllTextAsync());
            Assert.Equal("steam_api", await gameff.Combine("steam_api64.dll").ReadAllTextAsync());
            Assert.Equal(Hash.FromBase64("k5EWx/9Woqg="), await gameff.Combine(@"Data\Skyrim - Interface.bsa").FileHashAsync());

            var ini = tempFolder.Dir.Combine(Consts.ModOrganizer2Ini).LoadIniFile();
            Assert.Equal(gameff, (AbsolutePath)(string)ini.General.gamePath);
            Assert.Equal(gameff, (AbsolutePath)(string)ini.General.pathDouble);
            Assert.Equal(gameff, (AbsolutePath)(string)ini.General.pathForward);


        }

        public TasksTests(ITestOutputHelper helper) : base(helper)
        {
        }
    }
}
