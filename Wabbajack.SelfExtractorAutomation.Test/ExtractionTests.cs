using System;
using System.Linq;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.SelfExtractorAutomation.Steps;
using Xunit;

namespace Wabbajack.SelfExtractorAutomation.Test
{
    public class UnitTest1
    {
        [Fact]
        public async Task CanGetSteps()
        {

            var steps = new IAutomationStep[]
            {
                new SetEditContents {Name = "Extract to:", Value = "@OUTPUT_FOLDER@"},
                new ClickButton {Name = "Extract"}, 
            };

            var data = steps.ToJson();
            Assert.NotNull(data);
            Assert.NotNull(data.FromJsonString<IAutomationStep[]>());

            var tmp = await TempFolder.Create();
            var execution =
                new ExecutionEngine(@"Resources\WABBAJACK_TEST_FILE.exe".RelativeTo(AbsolutePath.EntryPoint), tmp.Dir,
                    steps);
            await execution.Run();

        }
        
        // Disabled Use only for debugging
        //[Fact]
        public async Task CanExecuteLongJob()
        {

            var exe =
                (AbsolutePath)@"D:\witcher_mods\The Witcher 3 HD Reworked Project (Part 1)-1021-11-0-1583980985.exe";
            var steps = new IAutomationStep[]
            {
                new ClickButton {Name = "OK"}, 
                new ClickButton {Name = "Next >"}, 
                new ClickButton {Name = "Next >"}, 
                new ClickButton {Name = "Next >"},
                new SetEditContents {Name = "Destination directory", Value = "@OUTPUT_FOLDER@"},
                new ClickButton {Name = "Next >"},
                new ClickButton {Name = "Next >"},
                new TrackProgress(),
                new ClickButton {Name = "Finish"}
            };

            var data = steps.ToJson();
            Assert.NotNull(data);
            Assert.NotNull(data.FromJsonString<IAutomationStep[]>());

            var tmp = await TempFolder.Create();
            using (var execution =
                new ExecutionEngine(exe, tmp.Dir, steps))
            {
                await execution.Run();
            }

            using var queue = new WorkQueue();
            var results = await tmp.Dir.EnumerateFiles().OrderBy(f => f.Size)
                .Where(f => f.Extension != new Extension(".dat"))
                .PMap(queue, async f => await f.FileHashAsync());
            Assert.Equal(new [] {"2LBRhoIycKY=","8BbGu+3upLM=","itOAqTzG8j8=","g8BN6zAVtGc=","tIztd5mycMM=","Z7u2C+DlrpI=","AtcPLeI541A=","FsQMQWhr8p8=","p3fH+auhMgs=","LRq1hwdsfrU=","U/+Fy5cVnzk="}.Select(Hash.FromBase64).ToArray()
                , results);
        }
        
        
    }
}
