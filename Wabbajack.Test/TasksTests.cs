using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Lib.Tasks;
using Xunit;

namespace Wabbajack.Test
{
    public class TasksTests
    {
        [Fact]
        public async Task CanRemapGameFolder()
        {
            await using var tempFolder = await TempFolder.Create();

            await tempFolder.Dir.Combine("some_file.txt").WriteAllTextAsync("some_file");
            await tempFolder.Dir.Combine("steam_api64.dll").WriteAllTextAsync("steam_api");
            

            var meta = Game.SkyrimSpecialEdition.MetaData(); 
            await tempFolder.Dir.Combine(Consts.ModOrganizer2Ini)
                .WriteAllLinesAsync(
                    "[General]",
                    $"gameName={meta.MO2Name}",
                    $"gamePath={meta.GameLocation()}",
                    $"pathDouble={meta.GameLocation().ToString().Replace(@"\", @"\\")}",
                    $"pathForward={meta.GameLocation().ToString().Replace(@"\", @"/")}");

            await MigrateGameFolder.Execute(tempFolder.Dir);
            
            


        }
    }
}
