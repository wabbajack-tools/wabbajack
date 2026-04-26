using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Compression.Zip;
using Wabbajack.Hashing.xxHash64;
using Xunit;

namespace Wabbajack.Networking.Http.Test;

public class ChunkedBufferingStreamTests
{
    private readonly Random _random;
    private readonly byte[] _data;
    private readonly MemoryStream _mstream;

    public ChunkedBufferingStreamTests()
    {
        _random = new Random();
        _data = new byte[1024 * 1024 * 24];
        _random.NextBytes(_data);
        _mstream = new MemoryStream(_data);
    }
    
    [Fact]
    public async Task CanHashStream()
    {
        var cstream = new MemoryChunkedBufferingStream(_mstream);
        Assert.Equal(await _data.Hash(), await cstream.Hash(CancellationToken.None));
    }

    [Fact]
    public async Task CanExtractOneFile()
    {
        var ms = new MemoryStream();

        var files = Enumerable.Range(1, 10)
            .Select(f =>
            {
                var buffer = new byte[1024];
                _random.NextBytes(buffer);
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

            {
                var entry = zipFile.CreateEntry("ending", CompressionLevel.Optimal);
                await using var es = entry.Open();
                await es.WriteAsync(Encoding.UTF8.GetBytes("Cheese for Everyone!"));
            }
        }

        ms.Position = 0;
        await using (var zipFile = new ZipReader(new MemoryChunkedBufferingStream(ms)))
        {
            var entry = (await zipFile.GetFiles()).First(f => f.FileName == "ending");

            var ems = new MemoryStream();
            await zipFile.Extract(entry, ems, CancellationToken.None);
            Assert.Equal(Encoding.UTF8.GetBytes("Cheese for Everyone!"), ems.ToArray());
        }

        
    }
}