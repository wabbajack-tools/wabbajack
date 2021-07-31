using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.ModListRegistry;
using Wabbajack.Server.DataLayer;
using Wabbajack.Server.DTOs;
using Wabbajack.Server.Services;
using Xunit;
using Xunit.Abstractions;

namespace Wabbajack.BuildServer.Test
{
    public class ModListValidationTests : ABuildServerSystemTest
    {
        public ModListValidationTests(ITestOutputHelper output, SingletonAdaptor<BuildServerFixture> fixture) : base(output, fixture)
        {
        }
        
        [Fact]
        public async Task CanLoadMetadataFromTestServer()
        {
            var modlist = await MakeModList("CanLoadMetadataFromTestServer.txt");
            Consts.ModlistMetadataURL = modlist.ToString();
            var data = await ModlistMetadata.LoadFromGithub();
            Assert.Equal(3, data.Count);
            Assert.Equal("test_list", data.OrderByDescending(x => x.Links.MachineURL).First().Links.MachineURL);
        }

        [Fact]
        public async Task CanIngestModLists()
        {
            var modlist = await MakeModList("CanIngestModLists.txt");
            Consts.ModlistMetadataURL = modlist.ToString();
            var sql = Fixture.GetService<SqlService>();
            var downloader = Fixture.GetService<ModListDownloader>();
            await downloader.Execute();

            foreach (var list in ModListMetaData)
            {
                Assert.True(await sql.HaveIndexedModlist(list.Links.MachineURL, list.DownloadMetadata.Hash));
            }
            
            // Nothing has changed so we shouldn't be downloading anything this time
            // Test doesn't matter for now
            //Assert.Equal(0, await downloader.Execute());

        }
        
        [Fact]
        public async Task CanValidateModLists()
        {
            var sql = Fixture.GetService<SqlService>();
            await using var conn = await sql.Open();
            await conn.ExecuteAsync("DELETE from Patches");

            var modlists = await MakeModList("CanValidateModlistsFile.txt");
            Consts.ModlistMetadataURL = modlists.ToString();
            Utils.Log("Updating modlists");
            await RevalidateLists(true);
            
            Utils.Log("Checking validated results");
            var data = (await ModlistMetadata.LoadFromGithub()).FirstOrDefault(l => l.Links.MachineURL == "test_list");
            Assert.NotNull(data);
            Assert.Equal(0, data.ValidationSummary.Failed);
            Assert.Equal(1, data.ValidationSummary.Passed);
            
            await CheckListFeeds(0, 1);

            Utils.Log("Break List");
            var archive = "CanValidateModlistsFile.txt".RelativeTo(Fixture.ServerPublicFolder);
            await archive.MoveToAsync(archive.WithExtension(new Extension(".moved")), true);

            // We can revalidate but the non-nexus archives won't be checked yet since the list didn't change
            await RevalidateLists(false);
            
            data = (await ModlistMetadata.LoadFromGithub()).FirstOrDefault(l => l.Links.MachineURL == "test_list");
            Assert.NotNull(data);
            Assert.Equal(0, data.ValidationSummary.Failed);
            Assert.Equal(1, data.ValidationSummary.Passed);

            // Run the non-nexus validator
            await RevalidateLists(true);

            data = (await ModlistMetadata.LoadFromGithub()).FirstOrDefault(l => l.Links.MachineURL == "test_list");
            Assert.NotNull(data);
            Assert.Equal(1, data.ValidationSummary.Failed);
            Assert.Equal(0, data.ValidationSummary.Passed);
            
            await CheckListFeeds(1, 0);
            
            Utils.Log("Fix List");
            await archive.WithExtension(new Extension(".moved")).MoveToAsync(archive, false);

            await RevalidateLists(true);

            data = (await ModlistMetadata.LoadFromGithub()).FirstOrDefault(l => l.Links.MachineURL == "test_list");
            Assert.NotNull(data);
            Assert.Equal(0, data.ValidationSummary.Failed);
            Assert.Equal(1, data.ValidationSummary.Passed);
            
            await CheckListFeeds(0, 1);

        }
        
                [Fact]
        public async Task CanHealLists()
        {
            var modlists = await MakeModList("CanHealLists.txt");
            Consts.ModlistMetadataURL = modlists.ToString();
            Utils.Log("Updating modlists");
            await RevalidateLists(true);
            
            Utils.Log("Checking validated results");
            var data = (await ModlistMetadata.LoadFromGithub()).FirstOrDefault(l => l.Links.MachineURL == "test_list");
            Assert.NotNull(data);
            Assert.Equal(0, data.ValidationSummary.Failed);
            Assert.Equal(1, data.ValidationSummary.Passed);
            
            await CheckListFeeds(0, 1);

            Utils.Log("Break List by changing the file");
            var archive = "CanHealLists.txt".RelativeTo(Fixture.ServerPublicFolder);
            await archive.WriteAllTextAsync("broken");

            // We can revalidate but the non-nexus archives won't be checked yet since the list didn't change
            await RevalidateLists(false);
            
            data = (await ModlistMetadata.LoadFromGithub()).FirstOrDefault(l => l.Links.MachineURL == "test_list");
            Assert.NotNull(data);
            Assert.Equal(0, data.ValidationSummary.Failed);
            Assert.Equal(1, data.ValidationSummary.Passed);

            // Run the non-nexus validator
            await RevalidateLists(true);

            data = (await ModlistMetadata.LoadFromGithub()).FirstOrDefault(l => l.Links.MachineURL == "test_list");
            Assert.NotNull(data);
            Assert.Equal(1, data.ValidationSummary.Failed);
            Assert.Equal(0, data.ValidationSummary.Passed);
            Assert.Equal(1, data.ValidationSummary.Updating);

            var patcher = Fixture.GetService<PatchBuilder>();
            Assert.True(await patcher.Execute() > 1);

            await RevalidateLists(false);
            
            data = (await ModlistMetadata.LoadFromGithub()).FirstOrDefault(l => l.Links.MachineURL == "test_list");
            Assert.NotNull(data);
            Assert.Equal(0, data.ValidationSummary.Failed);
            Assert.Equal(1, data.ValidationSummary.Passed);
            Assert.Equal(0, data.ValidationSummary.Updating);
        }
        
        private async Task RevalidateLists(bool runNonNexus)
        {
            
            var downloader = Fixture.GetService<ModListDownloader>();
            await downloader.Execute();

            if (runNonNexus)
            {
                var nonNexus = Fixture.GetService<NonNexusDownloadValidator>();
                await nonNexus.Execute();
            }

            var validator = Fixture.GetService<ListValidator>();
            await validator.Execute();

            var archiver = Fixture.GetService<ArchiveDownloader>();
            await archiver.Execute();
        }

        private async Task CheckListFeeds(int failed, int passed)
        {
            var statusJson = await _client.GetJsonAsync<DetailedStatus>(MakeURL("lists/status/test_list.json"));
            Assert.Equal(failed, statusJson.Archives.Count(a => a.IsFailing));
            Assert.Equal(passed, statusJson.Archives.Count(a => !a.IsFailing));

            
            var statusHtml = await _client.GetHtmlAsync(MakeURL("lists/status/test_list.html"));
            Assert.NotEmpty(statusHtml.DocumentNode.Descendants().Where(n => n.InnerHtml == $"Failed ({failed}):"));
            Assert.NotEmpty(statusHtml.DocumentNode.Descendants().Where(n => n.InnerHtml == $"Passed ({passed}):"));
            
            var statusRss = await _client.GetHtmlAsync(MakeURL("lists/status/test_list/broken.rss"));
            Assert.Equal(failed, statusRss.DocumentNode.SelectNodes("//item")?.Count ?? 0);

            var heartBeat = await _client.GetHtmlAsync(MakeURL("heartbeat/report"));
            Assert.Contains(heartBeat.DocumentNode.Descendants(), c => c.InnerText.StartsWith("test_list"));
        }

        


    }
}
