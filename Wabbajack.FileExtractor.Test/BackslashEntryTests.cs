using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Xunit;

namespace Wabbajack.FileExtractor.Test;

/// <summary>
///     Zips written on Windows by tools that put backslashes in entry names, which the format does not allow but
///     Windows reads as folders. Off Windows 7-Zip writes such an entry as one file whose name holds the
///     backslashes, and Wabbajack's paths split on them, so the file was looked for in a folder that was never
///     made. Synthesis.zip (Mutagen Synthesis 0.36.6) is one, and failed every install that used it on Linux.
/// </summary>
public class BackslashEntryTests
{
    private readonly FileExtractor _extractor;
    private readonly TemporaryFileManager _manager;

    public BackslashEntryTests(FileExtractor extractor, TemporaryFileManager manager)
    {
        _extractor = extractor;
        _manager = manager;
    }

    private async Task<AbsolutePath> ZipWithBackslashNames()
    {
        var folder = _manager.CreateFolder().Path;
        var zip = folder.Combine("backslashes.zip");
        await using (var fs = File.Create(zip.ToString()))
        using (var archive = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            foreach (var (name, data) in new[]
                     {
                         (@"BuildHost-net472\Host.exe.config", "config"),
                         (@"BuildHost-netcore\deep\Host.deps.json", "deps"),
                         ("plain.txt", "plain")
                     })
            {
                await using var entry = archive.CreateEntry(name).Open();
                await using var writer = new StreamWriter(entry);
                await writer.WriteAsync(data);
            }
        }

        return zip;
    }

    [Fact]
    public async Task EntriesNamedWithBackslashesAreFoundInTheirFolders()
    {
        var zip = await ZipWithBackslashNames();
        var wanted = new[]
        {
            @"BuildHost-net472\Host.exe.config".ToRelativePath(), @"BuildHost-netcore\deep\Host.deps.json".ToRelativePath()
        };

        var results = await _extractor.GatheringExtractWith7Zip(new NativeFileStreamFactory(zip),
            path => wanted.Contains(path),
            async (path, file) =>
            {
                await using var s = await file.GetStream();
                using var reader = new StreamReader(s);
                return await reader.ReadToEndAsync();
            }, wanted, CancellationToken.None);

        Assert.Equal("config", results[wanted[0]]);
        Assert.Equal("deps", results[wanted[1]]);
    }

    [Fact]
    public async Task EveryEntryOfSuchAZipExtracts()
    {
        var zip = await ZipWithBackslashNames();

        var results = await _extractor.GatheringExtract(new NativeFileStreamFactory(zip), _ => true,
            async (path, file) =>
            {
                await using var s = await file.GetStream();
                using var reader = new StreamReader(s);
                return await reader.ReadToEndAsync();
            }, CancellationToken.None);

        Assert.Equal(new[] {"config", "deps", "plain"}, results.Values.OrderBy(v => v));
    }
}
