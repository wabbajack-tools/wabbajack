using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Wabbajack.Compression.Zip.Test
{
    public class Tests
    {
        [Test]
        public async Task CanReadSimpleZip()
        {
            var random = new Random();
            
            var ms = new MemoryStream();

            var files = Enumerable.Range(1, 10)
                .Select(f =>
                {
                    var buffer = new byte[1024];
                    random.NextBytes(buffer);
                    return (f, buffer);
                }).ToArray();

            using (var zipFile = new ZipArchive(ms, ZipArchiveMode.Create, true))
            {
                foreach (var (f, buffer) in files)
                {
                    var entry = zipFile.CreateEntry(f.ToString(), CompressionLevel.Optimal);
                    await using var es = entry.Open();
                    await es.WriteAsync(buffer);
                }
            }

            ms.Position = 0;

            var reader = new ZipReader(ms);
            foreach (var file in (await reader.GetFiles()).Zip(files))
            {
                var tms = new MemoryStream();
                await reader.Extract(file.First, tms, CancellationToken.None);
                await Assert.That(file.Second.f.ToString()).IsEqualTo(file.First.FileName);
                await Assert.That(tms.ToArray().SequenceEqual(file.Second.buffer)).IsTrue();
            }

        }
    }
}