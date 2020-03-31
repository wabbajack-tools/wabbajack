using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Wabbajack.BuildServer.Model.Models;
using Wabbajack.BuildServer.Models.Jobs;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.FileUploader;
using Xunit;
using Xunit.Abstractions;
using Xunit.Priority;

namespace Wabbajack.BuildServer.Test
{
    public class IndexedFilesTests : ABuildServerSystemTest
    {
        public IndexedFilesTests(ITestOutputHelper output, BuildServerFixture fixture) : base(output, fixture)
        {
        }

        [Fact, Priority(1)]
        public async Task CanIngestExportedInis()
        {
            var to = Fixture.ServerTempFolder.Combine("IniIngest");
            await @"sql\DownloadStates".RelativeTo(AbsolutePath.EntryPoint).CopyDirectoryToAsync(to);
            var result = await _authedClient.GetStringAsync(MakeURL("indexed_files/ingest/IniIngest"));
            Assert.Equal("5", result);
        }

        [Fact, Priority(2)]
        public async Task CanQueryViaHash()
        {
            var hashes = new HashSet<Hash>
            {
                Hash.FromHex("097ad17ef4b9f5b7"),
                Hash.FromHex("96fb53c3dc6397d2"),
                Hash.FromHex("97a6d27b7becba19")
            };

            foreach (var hash in hashes)
            {
                Utils.Log($"Testing Archive {hash}");
                var ini = await ClientAPI.GetModIni(hash);
                Assert.NotNull(ini);
                Assert.NotNull(DownloadDispatcher.ResolveArchive(ini.LoadIniString()));
            }
        }

        [Fact]
        public async Task CanNotifyOfInis()
        {
            var files = await @"sql\NotifyStates".RelativeTo(AbsolutePath.EntryPoint)
                .EnumerateFiles()
                .Where(f => f.Extension == Consts.IniExtension)
                .PMap(Queue, async ini => (AbstractDownloadState)(await DownloadDispatcher.ResolveArchive(ini.LoadIniFile())));

            var archives = files.Select(f =>
                new Archive
                {
                    State = f,
                    Name = Guid.NewGuid().ToString()
                });
            Assert.True(await AuthorAPI.UploadPackagedInis(archives));

            var SQL = Fixture.GetService<SqlService>();
            var job = await SQL.GetJob();
            Assert.IsType<IndexJob>(job.Payload);
            var payload = (IndexJob)job.Payload;
            
            Assert.IsType<NexusDownloader.State>(payload.Archive.State);

            var casted = (NexusDownloader.State)payload.Archive.State;
            Assert.Equal(Game.Skyrim, casted.Game);
            
            // Insert the record into SQL
            await SQL.AddDownloadState(Hash.FromHex("00e8bbbf591f61a3"), casted);

            // Enqueue the same file again
            Assert.True(await AuthorAPI.UploadPackagedInis(archives));
            
            // File is aleady indexed so nothing gets enqueued
            Assert.Null(await SQL.GetJob());
        }
    }
}
