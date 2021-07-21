using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Documents;
using Octokit;
using Wabbajack.Common;
using Wabbajack.Lib.GitHub;
using Wabbajack.Lib.ModListRegistry;
using Xunit;

namespace Wabbajack.Test
{
    public class GitHubTests
    {
        //[Fact]// Disabled for build server, run locally
        public async Task CanLogIntoGithub()
        {
            var client = await Wabbajack.Lib.GitHub.Client.Get();
            var rnd = new Random();
            var meta = new DownloadMetadata
            {
                Hash = Hash.FromLong(rnd.Next()),
                NumberOfArchives = rnd.Next(100),
                NumberOfInstalledFiles = rnd.Next(1000),
                SizeOfInstalledFiles = rnd.Next(1000000),
                Size = rnd.Next(10000),

            };
            var update = new UpdateRequest
            {
                DownloadMetadata = meta,
                DownloadUrl = new Uri($"https://www.google.com/{rnd.Next()}"),
                MachineUrl = "ci_test",
                Version = new Version(1, rnd.Next(10), rnd.Next(10), rnd.Next(10))
            };
            await client.UpdateList("ci_tester", update);

            var updated = await client.GetData(Client.List.CI);
            var lst = updated.Lists.FirstOrDefault(l => l.Links.MachineURL == "ci_test");
            var newMeta = lst!.DownloadMetadata!;
            Assert.Equal(meta.Hash, newMeta.Hash);
            Assert.Equal(meta.Size, newMeta.Size);
            Assert.Equal(update.Version, lst.Version);
            
            Assert.Equal(meta.NumberOfArchives, newMeta.NumberOfArchives);
            Assert.Equal(meta.NumberOfInstalledFiles, newMeta.NumberOfInstalledFiles);
            Assert.Equal(meta.SizeOfInstalledFiles, newMeta.SizeOfInstalledFiles);
        }
    }
}
