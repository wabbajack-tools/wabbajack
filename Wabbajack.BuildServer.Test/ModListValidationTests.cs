using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Wabbajack.BuildServer.BackendServices;
using Wabbajack.BuildServer.Controllers;
using Wabbajack.BuildServer.Model.Models;
using Wabbajack.BuildServer.Models;
using Wabbajack.BuildServer.Models.JobQueue;
using Wabbajack.BuildServer.Models.Jobs;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.FileUploader;
using Wabbajack.Lib.ModListRegistry;
using Xunit;
using Xunit.Abstractions;
using IndexedFile = Wabbajack.BuildServer.Models.IndexedFile;

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
            var modlist = await MakeModList();
            Consts.ModlistMetadataURL = modlist.ToString();
            var data = await ModlistMetadata.LoadFromGithub();
            Assert.Single(data);
            Assert.Equal("test_list", data.First().Links.MachineURL);
        }

        [Fact]
        public async Task CanIngestModLists()
        {
            var modlist = await MakeModList();
            Consts.ModlistMetadataURL = modlist.ToString();
            var sql = Fixture.GetService<SqlService>();
            var service = new ListIngest(sql, Fixture.GetService<AppSettings>());
            await service.Execute();

            foreach (var list in ModListMetaData)
            {
                Assert.True(await sql.HaveIndexedModlist(list.Links.MachineURL, list.DownloadMetadata.Hash));
            }
        }

        [Fact]
        public async Task CanValidateModLists()
        {
            await ClearJobQueue();
            var modlists = await MakeModList();
            Consts.ModlistMetadataURL = modlists.ToString();
            Utils.Log("Updating modlists");
            await RevalidateLists();
            
            ListValidation.ResetCache();

            Utils.Log("Checking validated results");
            var data = await ModlistMetadata.LoadFromGithub();
            Assert.Single(data);
            Assert.Equal(0, data.First().ValidationSummary.Failed);
            Assert.Equal(1, data.First().ValidationSummary.Passed);
            
            await CheckListFeeds(0, 1);

            Utils.Log("Break List");
            var archive = "test_archive.txt".RelativeTo(Fixture.ServerPublicFolder);
            await archive.MoveToAsync(archive.WithExtension(new Extension(".moved")), true);

            // We can revalidate but the non-nexus archives won't be checked yet since the list didn't change
            await RevalidateLists();
            
            data = await ModlistMetadata.LoadFromGithub();
            Assert.Single(data);
            Assert.Equal(0, data.First().ValidationSummary.Failed);
            Assert.Equal(1, data.First().ValidationSummary.Passed);

            // Run the non-nexus validator
            var evalService = new ValidateNonNexusArchives(Fixture.GetService<SqlService>(), Fixture.GetService<AppSettings>());
            await evalService.Execute();

            ListValidation.ResetCache();

            data = await ModlistMetadata.LoadFromGithub();
            Assert.Single(data);
            Assert.Equal(1, data.First().ValidationSummary.Failed);
            Assert.Equal(0, data.First().ValidationSummary.Passed);
            
            await CheckListFeeds(1, 0);
            
            Utils.Log("Fix List");
            await archive.WithExtension(new Extension(".moved")).MoveToAsync(archive, false);

            await RevalidateLists();
            // Rerun the validation service to fix the list
            await evalService.Execute();

            ListValidation.ResetCache();

            data = await ModlistMetadata.LoadFromGithub();
            Assert.Single(data);
            Assert.Equal(0, data.First().ValidationSummary.Failed);
            Assert.Equal(1, data.First().ValidationSummary.Passed);
            
            await CheckListFeeds(0, 1);

        }

        [Fact]
        public async Task CanUpgradeHttpDownloads()
        {
            await ClearJobQueue();
            var modlists = await MakeModList();

            await IndexFile(ModListData.Archives.First());
            
            Consts.ModlistMetadataURL = modlists.ToString();
            Utils.Log("Updating modlists");
            await RevalidateLists();

            Utils.Log("Checking validated results");
            var data = await ModlistMetadata.LoadFromGithub();
            Assert.Single(data);
            Assert.Equal(0, data.First().ValidationSummary.Failed);
            Assert.Equal(1, data.First().ValidationSummary.Passed);

            await CheckListFeeds(0, 1);
            
            var archive = "test_archive.txt".RelativeTo(Fixture.ServerPublicFolder);
            archive.Delete();
            await archive.WriteAllBytesAsync(Encoding.UTF8.GetBytes("More Cheese for Everyone!"));

            var evalService = new ValidateNonNexusArchives(Fixture.GetService<SqlService>(), Fixture.GetService<AppSettings>());
            await evalService.Execute();
            await RevalidateLists();
            
            ListValidation.ResetCache();

            Utils.Log("Checking updated results");
            data = await ModlistMetadata.LoadFromGithub();
            Assert.Single(data);
            Assert.Equal(0, data.First().ValidationSummary.Failed);
            Assert.Equal(0, data.First().ValidationSummary.Passed);
            Assert.Equal(1, data.First().ValidationSummary.Updating);

            await CheckListFeeds(1, 0);

        }

        private async Task IndexFile(Archive archive)
        {
            var job = new IndexJob {Archive = archive};
            await job.Execute(Fixture.GetService<SqlService>(), Fixture.GetService<AppSettings>());
        }

        private async Task RevalidateLists()
        {
            var sql = Fixture.GetService<SqlService>();
            var settings = Fixture.GetService<AppSettings>();

            var jobService = new ListIngest(sql, settings);
            await jobService.Execute();
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
        }


        private async Task<Uri> MakeModList()
        {
            var archive_data = Encoding.UTF8.GetBytes("Cheese for Everyone!");
            var test_archive_path = "test_archive.txt".RelativeTo(Fixture.ServerPublicFolder);
            await test_archive_path.WriteAllBytesAsync(archive_data);



            ModListData = new ModList();
            ModListData.Archives.Add(
                new Archive(new HTTPDownloader.State(MakeURL("test_archive.txt")))
                {
                    Hash = await test_archive_path.FileHashAsync(),
                    Name = "test_archive",
                    Size = test_archive_path.Size,
                });
            
            var modListPath = "test_modlist.wabbajack".RelativeTo(Fixture.ServerPublicFolder);

            await using (var fs = modListPath.Create())
            {
                using var za = new ZipArchive(fs, ZipArchiveMode.Create);
                var entry = za.CreateEntry("modlist");
                await using var es = entry.Open();
                ModListData.ToJson(es);
            }

            ModListMetaData = new List<ModlistMetadata>
            {
                new ModlistMetadata
                {
                    Official = false,
                    Author = "Test Suite",
                    Description = "A test",
                    DownloadMetadata = new DownloadMetadata
                    {
                        Hash = await modListPath.FileHashAsync(), 
                        Size = modListPath.Size
                    },
                    Links = new ModlistMetadata.LinksObject
                    {
                        MachineURL = "test_list",
                        Download = MakeURL("test_modlist.wabbajack")
                    }
                }
            };

            var metadataPath = "test_mod_list_metadata.json".RelativeTo(Fixture.ServerPublicFolder);

            ModListMetaData.ToJson(metadataPath);
            
            return new Uri(MakeURL("test_mod_list_metadata.json"));
        }

        public ModList ModListData { get; set; }

        public List<ModlistMetadata> ModListMetaData { get; set; }
    }
}
