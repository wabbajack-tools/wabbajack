using System;
using System.IO.Compression;
using System.Threading.Tasks;
using Wabbajack.Common;
using Xunit;

namespace Wabbajack.VirtualFileSystem.Test
{
    public class FileExtractorTests
    {
        [Fact]
        public async Task CanGatherDataFromZipFiles()
        {
            await using var temp = await TempFolder.Create();
            await using var archive = new TempFile();
            for (int i = 0; i < 10; i ++)
            {
                await WriteRandomData(temp.Dir.Combine($"{i}.bin"), _rng.Next(10, 1024));
            }

            await ZipUpFolder(temp.Dir, archive.Path, false);
            
            var results = await FileExtractor2.GatheringExtract(new NativeFileStreamFactory(archive.Path), 
                _ => true,
                async (path, sfn) =>
            {
                await using var s = await sfn.GetStream();
                return await s.xxHashAsync();
            });
            
            Assert.Equal(10, results.Count);
            foreach (var (path, hash) in results)
            {
                Assert.Equal(await temp.Dir.Combine(path).FileHashAsync(), hash);
            }


        }


        private static readonly Random _rng = new Random();
        private static async Task WriteRandomData(AbsolutePath path, int size)
        {
            var buff = new byte[size];
            _rng.NextBytes(buff);
            await path.WriteAllBytesAsync(buff);
        }
        
        private static async Task AddFile(AbsolutePath filename, string text)
        {
            filename.Parent.CreateDirectory();
            await filename.WriteAllTextAsync(text);
        }

        private static async Task ZipUpFolder(AbsolutePath folder, AbsolutePath output, bool deleteSource = true)
        {
            ZipFile.CreateFromDirectory((string)folder, (string)output);
            if (deleteSource) 
                await folder.DeleteDirectory();
        }
        
    }
}
