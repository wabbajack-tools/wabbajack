using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.ModListRegistry;
using Wabbajack.Server.DataLayer;
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
            var downloader = Fixture.GetService<ModListDownloader>();
            Assert.Equal(2, await downloader.CheckForNewLists());

            foreach (var list in ModListMetaData)
            {
                Assert.True(await sql.HaveIndexedModlist(list.Links.MachineURL, list.DownloadMetadata.Hash));
            }
            
            // Nothing has changed so we shouldn't be downloading anything this time
            Assert.Equal(0, await downloader.CheckForNewLists());

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
                },
                new ModlistMetadata
                {
                    Official = true,
                    Author = "Test Suite",
                    Description = "A list with a broken hash",
                    DownloadMetadata = new DownloadMetadata()
                    {
                        Hash = Hash.FromLong(42),
                        Size = 42
                    },
                    Links = new ModlistMetadata.LinksObject
                    {
                        MachineURL = "broken_list",
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
