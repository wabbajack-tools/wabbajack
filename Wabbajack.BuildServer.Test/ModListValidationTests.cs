using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using HtmlAgilityPack;
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
        public async Task CanValidateModLists()
        {
            var modlists = await MakeModList();
            Consts.ModlistMetadataURL = modlists.ToString();
            Utils.Log("Updating modlists");
            await RevalidateLists();

            Utils.Log("Checking validated results");
            var data = await ModlistMetadata.LoadFromGithub();
            Assert.Single(data);
            Assert.Equal(0, data.First().ValidationSummary.Failed);
            Assert.Equal(1, data.First().ValidationSummary.Passed);
            
            await CheckListFeeds(0, 1);

            Utils.Log("Break List");
            var archive = "test_archive.txt".RelativeTo(Fixture.ServerPublicFolder);
            await archive.MoveToAsync(archive.WithExtension(new Extension(".moved")), true);

            await RevalidateLists();
            
            data = await ModlistMetadata.LoadFromGithub();
            Assert.Single(data);
            Assert.Equal(1, data.First().ValidationSummary.Failed);
            Assert.Equal(0, data.First().ValidationSummary.Passed);
            
            await CheckListFeeds(1, 0);
            
            Utils.Log("Fix List");
            await archive.WithExtension(new Extension(".moved")).MoveToAsync(archive, false);

            await RevalidateLists();
            
            data = await ModlistMetadata.LoadFromGithub();
            Assert.Single(data);
            Assert.Equal(0, data.First().ValidationSummary.Failed);
            Assert.Equal(1, data.First().ValidationSummary.Passed);
            
            await CheckListFeeds(0, 1);

        }

        private async Task RevalidateLists()
        {
            var result = await AuthorAPI.UpdateServerModLists();
            Assert.NotNull(result);
            
            var sql = Fixture.GetService<SqlService>();
            var settings = Fixture.GetService<AppSettings>();
            var job = await sql.GetJob();

            Assert.NotNull(job);
            Assert.IsType<UpdateModLists>(job.Payload);


            var jobResult = await job.Payload.Execute(sql, settings);
            Assert.Equal(JobResultType.Success, jobResult.ResultType);
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



            var modListData = new ModList
            {
                Archives = new List<Archive>
                {
                    new Archive
                    {
                        Hash = await test_archive_path.FileHashAsync(),
                        Name = "test_archive",
                        Size = test_archive_path.Size,
                        State = new HTTPDownloader.State {Url = MakeURL("test_archive.txt")}
                    }
                }
            };
            
            var modListPath = "test_modlist.wabbajack".RelativeTo(Fixture.ServerPublicFolder);

            await using (var fs = modListPath.Create())
            {
                using var za = new ZipArchive(fs, ZipArchiveMode.Create);
                var entry = za.CreateEntry("modlist.json");
                await using var es = entry.Open();
                modListData.ToJson(es);
            }

            var modListMetaData = new List<ModlistMetadata>
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

            modListMetaData.ToJson(metadataPath);
            
            return new Uri(MakeURL("test_mod_list_metadata.json"));
        }
    }
}
