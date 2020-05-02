using System;
using System.Net.Http;
using System.Threading.Tasks;
using Wabbajack.BuildServer.Model.Models;
using Wabbajack.BuildServer.Models;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.FileUploader;
using Xunit;
using Xunit.Abstractions;
using Xunit.Priority;

namespace Wabbajack.BuildServer.Test
{
    [Collection("ServerTests")]
    public class UploadedFilesTest : ABuildServerSystemTest
    {
        [Fact, Priority(1)]
        public async Task CanIngestMongoDBExports()
        {
            var data = await @"sql\uploaded_files_ingest.json".RelativeTo(AbsolutePath.EntryPoint).ReadAllTextAsync();
            data = data.Replace("<testuser>", Fixture.User);
            await Fixture.ServerTempFolder.Combine("uploaded_files_ingest.json").WriteAllTextAsync(data);
            using var response = await _authedClient.GetAsync(MakeURL("ingest/uploaded_files/uploaded_files_ingest.json"));
            var result = await response.Content.ReadAsStringAsync();
            Utils.Log("Loaded: " + result);

            
            Assert.Equal("4", result);
        }
        
        [Fact, Priority(1)]
        public async Task CanListMyUploadedFiles()
        {
            var result = (await _authedClient.GetStringAsync(MakeURL("uploaded_files/list"))).FromJsonString<string[]>();
            Utils.Log("Loaded: " + result);
            
            
            Assert.True(result.Length >= 2, result.Length.ToString());
            Assert.Contains("file1-90db7c47-a8ae-4a62-9c2e-b7d357a16665.zip", result);
            Assert.Contains("file2-63f8f868-0f4d-4997-922b-ee952984973a.zip", result);
            // These are from other users
            Assert.DoesNotContain("file2-1f18f301-67eb-46c9-928a-088f6666bf61.zip", result);
            Assert.DoesNotContain("file3-17b3e918-8409-48e6-b7ff-6af858bfd1ba.zip", result);
        }

        [Fact]
        public async Task CanUploadFilesUsingClientApi()
        {
            using (var file = new TempFile())
            {
                var data = new byte[1024 * 1024 * 8 * 4];
                await using (var fs = file.Path.Create())
                {
                    await fs.WriteAsync(data);
                }

                Utils.Log($"Uploading {file.Path.Size.ToFileSizeString()} file");
                var result = await AuthorAPI.UploadFile(file.Path,
                    progress => Utils.Log($"Uploading : {progress * 100}%"), Fixture.APIKey);

                Utils.Log($"Result {result}");
                Assert.StartsWith("https://wabbajackpush.b-cdn.net/" +(string)file.Path.FileNameWithoutExtension, result);
            }

        }
        
        [Fact]
        public async Task CanDeleteFilesUsingClientApi()
        {
            using (var file = new TempFile())
            {
                var data = new byte[1024];
                await using (var fs = file.Path.Create())
                {
                    await fs.WriteAsync(data);
                }

                Utils.Log($"Uploading {file.Path.Size.ToFileSizeString()} file");
                var result = await AuthorAPI.UploadFile(file.Path,
                    progress => Utils.Log($"Uploading : {progress * 100}%"), Fixture.APIKey);

                Utils.Log($"Delete {result}");
                await AuthorAPI.DeleteFile((string)((RelativePath)new Uri(result).AbsolutePath).FileName);

            }

        }

        public UploadedFilesTest(ITestOutputHelper output, SingletonAdaptor<BuildServerFixture> fixture) : base(output, fixture)
        {
        }
    }
}
