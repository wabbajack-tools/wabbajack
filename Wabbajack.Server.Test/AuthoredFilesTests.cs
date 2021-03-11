using System;
using System.Linq;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Common.Exceptions;
using Wabbajack.Lib;
using Wabbajack.Lib.AuthorApi;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Server.DataLayer;
using Wabbajack.Server.Services;
using Xunit;
using Xunit.Abstractions;

namespace Wabbajack.BuildServer.Test
{
    public class AuthoredFilesTests : ABuildServerSystemTest
    {
        public AuthoredFilesTests(ITestOutputHelper output, SingletonAdaptor<BuildServerFixture> fixture) : base(output, fixture)
        {
        }

        [Fact]
        public async Task CanUploadDownloadAndDeleteAuthoredFiles()
        {
            var cleanup = Fixture.GetService<AuthoredFilesCleanup>();
            var sql = Fixture.GetService<SqlService>();
            
            var toDelete = await cleanup.FindFilesToDelete();
            
            await using var file = new TempFile();
            await file.Path.WriteAllBytesAsync(RandomData(Consts.UPLOADED_FILE_BLOCK_SIZE * 4 + Consts.UPLOADED_FILE_BLOCK_SIZE / 3));
            var originalHash = await file.Path.FileHashAsync();

            var client = await Client.Create(Fixture.APIKey);
            using var queue = new WorkQueue(2);
            var uri = await client.UploadFile(queue, file.Path, (s, percent) => Utils.Log($"({percent}) {s}"));

            var data = (await Fixture.GetService<SqlService>().AllAuthoredFiles()).ToArray();
            Assert.Contains((string)file.Path.FileName, data.Select(f => f.OriginalFileName));

            var listing = await cleanup.GetCDNMungedNames();
            foreach (var d in data)
            {
                Assert.Contains(d.MungedName, listing);
            }

            // Just uploaded it, so it shouldn't be marked for deletion
            toDelete = await cleanup.FindFilesToDelete();
            foreach (var d in data)
            {
                Assert.DoesNotContain(d.MungedName, toDelete.CDNDelete);                
                Assert.DoesNotContain(d.ServerAssignedUniqueId, toDelete.SQLDelete);
            }

            var result = await _client.GetStringAsync(MakeURL("authored_files"));
            Assert.Contains((string)file.Path.FileName, result);

            var state = await DownloadDispatcher.Infer(uri);
            Assert.IsType<WabbajackCDNDownloader.State>(state);

            await state.Download(new Archive(state) {Name = (string)file.Path.FileName}, file.Path);
            Assert.Equal(originalHash, await file.Path.FileHashAsync());
            
            // Mark it as old
            foreach (var d in data)
            {
                await sql.TouchAuthoredFile(await sql.GetCDNFileDefinition(d.ServerAssignedUniqueId), DateTime.Now - TimeSpan.FromDays(8));
            }

            // Now it should be marked for deletion
            toDelete = await cleanup.FindFilesToDelete();
            foreach (var d in data)
            {
                Assert.Contains(d.MungedName, toDelete.CDNDelete);                
                Assert.Contains(d.ServerAssignedUniqueId, toDelete.SQLDelete);
            }

            await cleanup.Execute();
            
            toDelete = await cleanup.FindFilesToDelete();
            
            Assert.Empty(toDelete.CDNDelete);
            Assert.Empty(toDelete.SQLDelete);

        }
    }
}
