using System.IO.Compression;
using Microsoft.Extensions.Logging.Abstractions;
using Wabbajack.DTOs;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using Wabbajack.Translation.Nexus;
using Xunit;

namespace Wabbajack.Translation.Test;

public class TranslationDownloaderTests : IDisposable
{
    private readonly AbsolutePath _root =
        Path.Combine(Path.GetTempPath(), "wj-translation-dl-" + Guid.NewGuid().ToString("N")).ToAbsolutePath();

    public void Dispose()
    {
        if (_root.DirectoryExists()) Directory.Delete(_root.ToString(), true);
    }

    [Fact]
    public async Task ExtractsPluginsAndStringsButNotFoldersNamedAfterPlugins()
    {
        _root.CreateDirectory();
        var archive = _root.Combine("translation.zip");
        using (var zip = ZipFile.Open(archive.ToString(), ZipArchiveMode.Create))
        {
            void Add(string name, string text)
            {
                using var writer = new StreamWriter(zip.CreateEntry(name).Open());
                writer.Write(text);
            }

            Add("Data/Legacy.esm", "plugin");
            Add("Data/Strings/Legacy_french.STRINGS", "strings");
            Add("Data/sound/voice/Legacy.esm/maleunique/line_1.fuz", "voice");
            Add("Data/meshes/actors/character/facegendata/facegeom/Legacy.esm/00001.nif", "mesh");
            Add("Data/textures/big.dds", "texture");
        }

        var file = new TranslationFile(Game.SkyrimSpecialEdition, 1, 2, "Legacy FR", "translation.zip", "Legacy FR",
            "MAIN", archive.Size(), DateTime.UtcNow, ["Legacy.esm"]);
        var downloader = new TranslationDownloader(null!, null!, null!,
            new Resource<FileExtractor.FileExtractor>("File Extractor"), NullLogger<TranslationDownloader>.Instance);
        var work = _root.Combine("work");

        var candidates = await downloader.ExtractPlugins(new Dictionary<TranslationFile, AbsolutePath> {[file] = archive},
            work, null, CancellationToken.None);

        var candidate = Assert.Single(candidates);
        Assert.Equal("Legacy.esm", candidate.Plugin);
        var extracted = work.EnumerateFiles(recursive: true).Select(f => f.FileName.ToString()).OrderBy(n => n, StringComparer.Ordinal).ToList();
        Assert.Equal(["Legacy.esm", "Legacy_french.STRINGS"], extracted);
    }
}
