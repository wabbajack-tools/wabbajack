using System;
using System.Threading.Tasks;
using Wabbajack.BuildServer.Test;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Server.DataLayer;
using Wabbajack.Server.DTOs;
using Wabbajack.Server.Services;
using Xunit;
using Xunit.Abstractions;

namespace Wabbajack.Server.Test
{
    public class MirroredFilesTests : ABuildServerSystemTest
    {
        public MirroredFilesTests(ITestOutputHelper output, SingletonAdaptor<BuildServerFixture> fixture) : base(output, fixture)
        {
        }

        [Fact]
        public async Task CanUploadAndDownloadMirroredFiles()
        {
            var file = new TempFile();
            await file.Path.WriteAllBytesAsync(RandomData(1024 * 1024 * 6));
            var dataHash = await file.Path.FileHashAsync();
            Assert.NotNull(dataHash);

            await Fixture.GetService<ArchiveMaintainer>().Ingest(file.Path);
            Assert.True(Fixture.GetService<ArchiveMaintainer>().HaveArchive(dataHash!.Value));

            var sql = Fixture.GetService<SqlService>();
            
            await sql.UpsertMirroredFile(new MirroredFile
            {
                Created = DateTime.UtcNow,
                Rationale = "Test File", 
                Hash = dataHash!.Value
            });

            var uploader = Fixture.GetService<MirrorUploader>();
            Assert.Equal(1, await uploader.Execute());
            
            
            var archive = new Archive(new HTTPDownloader.State(MakeURL(dataHash.ToString())))
            {
                Hash = dataHash!.Value,
                Size = file.Path.Size
            };
            
            await using var file2 = new TempFile();
            await DownloadDispatcher.DownloadWithPossibleUpgrade(archive, file2.Path);
            Assert.Equal(dataHash!.Value, await file2.Path.FileHashAsync());
        }

        [Fact]
        public async Task CanQueueFiles()
        {
            var service = Fixture.GetService<MirrorQueueService>();
            Assert.Equal(1, await service.Execute());
        }
        
    }
}
