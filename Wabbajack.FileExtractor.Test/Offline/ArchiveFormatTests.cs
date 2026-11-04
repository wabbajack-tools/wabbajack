#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.DTOs.Streams;
using Wabbajack.FileExtractor.ExtractedFiles;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Xunit;

namespace Wabbajack.FileExtractor.Test.Offline;

/// <summary>
///     Every format <see cref="FileExtractor.GatheringExtract{T}" /> dispatches to, opened from an archive
///     written by <see cref="TestArchives" />, plus the ways it refuses one.
/// </summary>
public class ArchiveFormatTests : IAsyncLifetime
{
    private readonly FileExtractor _extractor;
    private readonly TemporaryFileManager _manager;
    private TemporaryPath _folder;

    public ArchiveFormatTests(FileExtractor extractor, TemporaryFileManager manager)
    {
        _extractor = extractor;
        _manager = manager;
    }

    public Task InitializeAsync()
    {
        _folder = _manager.CreateFolder();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _folder.DisposeAsync();
    }

    private AbsolutePath File(string name) => _folder.Path.Combine(name);

    private Task<IDictionary<RelativePath, string>> Read(IStreamFactory source,
        Predicate<RelativePath>? shouldExtract = null, HashSet<RelativePath>? onlyFiles = null)
    {
        return _extractor.GatheringExtract(source, shouldExtract ?? (_ => true), async (_, file) =>
        {
            await using var stream = await file.GetStream();
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }, CancellationToken.None, onlyFiles);
    }

    private Task<IDictionary<RelativePath, string>> Read(AbsolutePath archive,
        Predicate<RelativePath>? shouldExtract = null, HashSet<RelativePath>? onlyFiles = null)
    {
        return Read(new NativeFileStreamFactory(archive), shouldExtract, onlyFiles);
    }

    private static void AssertHolds(IDictionary<RelativePath, string> results, params (string Name, string Content)[] expected)
    {
        Assert.Equal(expected.Select(e => e.Name.ToRelativePath()).OrderBy(p => p),
            results.Keys.OrderBy(p => p));
        foreach (var (name, content) in expected)
            Assert.Equal(content, results[name.ToRelativePath()]);
    }

    [Fact]
    public async Task Zip_ExtractsEveryEntryUnderItsFolder()
    {
        var archive = await TestArchives.Zip(File("mod.zip"), TestArchives.Entries);

        AssertHolds(await Read(archive), TestArchives.Entries);
    }

    [Fact]
    public async Task Zip_ShouldExtractLeavesTheRestOut()
    {
        var archive = await TestArchives.Zip(File("mod.zip"), TestArchives.Entries);

        var results = await Read(archive, path => path.Extension == new Extension(".nif"));

        AssertHolds(results, TestArchives.Entries[0]);
    }

    [Fact]
    public async Task Zip_OnlyFilesExtractsJustThoseEntries()
    {
        var archive = await TestArchives.Zip(File("mod.zip"), TestArchives.Entries);
        var wanted = new HashSet<RelativePath> {TestArchives.Entries[0].Name.ToRelativePath(), "readme.txt".ToRelativePath()};

        var results = await Read(archive, onlyFiles: wanted);

        AssertHolds(results, TestArchives.Entries[0], TestArchives.Entries[2]);
    }

    /// <summary>
    ///     A count on its own cannot tell a damaged archive from a name that did not match, so the report has to
    ///     say what was asked for, what came out, and what 7-Zip said.
    /// </summary>
    [Fact]
    public async Task Zip_OnlyFilesNamingAMissingEntry_ReportsWhatWasAskedForAndWhatCameOut()
    {
        var archive = await TestArchives.Zip(File("mod.zip"), TestArchives.Entries);
        var wanted = new HashSet<RelativePath> {"readme.txt".ToRelativePath(), @"meshes\missing.nif".ToRelativePath()};

        var error = await Assert.ThrowsAnyAsync<Exception>(() => Read(archive, onlyFiles: wanted));

        Assert.Contains("1 results, expected 2", error.Message);
        Assert.Contains("requested (2)", error.Message);
        Assert.Contains(@"meshes\missing.nif", error.Message);
        Assert.Contains("extracted (1)", error.Message);
        Assert.Contains("7zip exit code", error.Message);
        Assert.Contains($"({archive.Size()} bytes)", error.Message);
    }

    /// <summary>
    ///     7-Zip 26 turns a backslash inside a stored name into an underscore, so an install asking for
    ///     <c>core\Requiem.esp</c> got "No files to process" and 0 of 832 files out of Requiem 5.0.3's bugfix pack.
    ///     Such an archive is read in process, with the backslashes as folders, as 7-Zip 20 read it.
    /// </summary>
    [Fact]
    public async Task SevenZip_WithBackslashNames_GivesTheFilesAnInstallAsksFor()
    {
        var archive = await TestArchives.SevenZipWithBackslashNames(File("requiem.7z"), TestArchives.Entries);
        var wanted = TestArchives.Entries.Select(e => e.Name.ToRelativePath()).ToHashSet();

        AssertHolds(await Read(archive, onlyFiles: wanted), TestArchives.Entries);
    }

    /// <summary>A compile extracts everything, and would otherwise index the files as core_Requiem.esp.</summary>
    [Fact]
    public async Task SevenZip_WithBackslashNames_ExtractsEverythingUnderItsFolders()
    {
        var archive = await TestArchives.SevenZipWithBackslashNames(File("requiem.7z"), TestArchives.Entries);

        AssertHolds(await Read(archive), TestArchives.Entries);
    }

    [Fact]
    public async Task SevenZip_WithBackslashNames_ShouldExtractLeavesTheRestOut()
    {
        var archive = await TestArchives.SevenZipWithBackslashNames(File("requiem.7z"), TestArchives.Entries);

        var results = await Read(archive, path => path.Extension == new Extension(".nif"));

        AssertHolds(results, TestArchives.Entries[0]);
    }

    [Fact]
    public async Task SevenZip_Extracts()
    {
        var archive = KnownFolders.EntryPoint.Combine("TestData", "cheese.7z");

        var results = await Read(archive);

        Assert.Single(results);
    }

    /// <summary>
    ///     A nested archive arrives as a stream rather than a file on disk; 7-Zip needs a file, so it is spooled to
    ///     one first.
    /// </summary>
    [Fact]
    public async Task ArchiveFromAStream_IsSpooledAndExtracted()
    {
        var archive = await TestArchives.Zip(File("inner.zip"), TestArchives.Entries);
        var source = new MemoryStreamFactory(ExtractedFileTests.Visible(await archive.ReadAllBytesAsync()),
            @"outer\inner.zip".ToRelativePath(), DateTime.UtcNow);

        AssertHolds(await Read(source), TestArchives.Entries);
    }

    [Fact]
    public async Task SkyrimBsa_Uncompressed_ExtractsEveryEntry()
    {
        var archive = await TestArchives.Bsa(File("mod.bsa"), _manager, false, TestArchives.Entries);

        AssertHolds(await Read(archive), TestArchives.Entries);
    }

    [Fact]
    public async Task SkyrimBsa_Compressed_ExtractsEveryEntry()
    {
        var archive = await TestArchives.Bsa(File("mod.bsa"), _manager, true, TestArchives.Entries);

        AssertHolds(await Read(archive), TestArchives.Entries);
    }

    [Fact]
    public async Task SkyrimBsa_ShouldExtractLeavesTheRestOut()
    {
        var archive = await TestArchives.Bsa(File("mod.bsa"), _manager, false, TestArchives.Entries);

        var results = await Read(archive, path => path.FileName == "readme.txt".ToRelativePath());

        AssertHolds(results, TestArchives.Entries[2]);
    }

    [Fact]
    public async Task Ba2_ExtractsEveryEntry()
    {
        var archive = await TestArchives.Ba2(File("mod - main.ba2"), _manager, TestArchives.Entries);

        AssertHolds(await Read(archive), TestArchives.Entries);
    }

    [Fact]
    public async Task MorrowindBsa_ExtractsEveryEntry()
    {
        var archive = await TestArchives.MorrowindBsa(File("mod.bsa"), TestArchives.Entries);

        AssertHolds(await Read(archive), TestArchives.Entries);
    }

    /// <summary>
    ///     A Morrowind BSA's signature is four bytes that plenty of other files start with, so it is only read as
    ///     an archive when it is named like one.
    /// </summary>
    [Fact]
    public async Task MorrowindSignatureWithoutABsaExtension_IsRefused()
    {
        var archive = await TestArchives.MorrowindBsa(File("mod.esp"), TestArchives.Entries);

        var error = await Assert.ThrowsAnyAsync<Exception>(() => Read(archive));

        Assert.Contains("Invalid file format", error.Message);
    }

    [Fact]
    public async Task UnrecognisedFile_IsRefused()
    {
        var file = File("notes.txt");
        await file.WriteAllTextAsync("not an archive of any kind");

        var error = await Assert.ThrowsAnyAsync<Exception>(() => Read(file));

        Assert.Contains("Invalid file format", error.Message);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task Btar_ExtractsEveryEntry(ushort minor)
    {
        var archive = await TestArchives.Btar(File("creation.ckm"), 1, minor, TestArchives.Entries);

        AssertHolds(await Read(archive), TestArchives.Entries);
    }

    [Fact]
    public async Task Btar_ShouldExtractSkipsOverTheRest()
    {
        var archive = await TestArchives.Btar(File("creation.ckm"), 1, 4, TestArchives.Entries);

        var results = await Read(archive, path => path.FileName == "readme.txt".ToRelativePath());

        AssertHolds(results, TestArchives.Entries[2]);
    }

    [Theory]
    [InlineData(2, 4, "major version")]
    [InlineData(1, 1, "minor version")]
    [InlineData(1, 5, "minor version")]
    public async Task Btar_UnsupportedVersion_IsRefused(ushort major, ushort minor, string expected)
    {
        var archive = await TestArchives.Btar(File("creation.ckm"), major, minor, TestArchives.Entries);

        var error = await Assert.ThrowsAnyAsync<Exception>(() => Read(archive));

        Assert.Contains(expected, error.Message);
    }

    /// <summary>
    ///     The installer moves an extracted file into place rather than copying it when it can, and a BTAR entry
    ///     says it can.
    /// </summary>
    [Fact]
    public async Task Btar_EntryMovesToDisk()
    {
        var archive = await TestArchives.Btar(File("creation.ckm"), 1, 4, TestArchives.Entries);
        var dest = _folder.Path.Combine("out");
        dest.CreateDirectory();

        await _extractor.GatheringExtract(new NativeFileStreamFactory(archive), _ => true, async (path, file) =>
        {
            Assert.True(file.CanMove);
            await file.MoveHashedAsync(dest.Combine(path.FileName), CancellationToken.None);
            return 0;
        }, CancellationToken.None);

        foreach (var (name, content) in TestArchives.Entries)
            Assert.Equal(content, await dest.Combine(name.ToRelativePath().FileName).ReadAllTextAsync());
    }

    /// <summary>
    ///     Every .exe is offered to innoextract, and most are not installers. That is an ordinary answer: nothing
    ///     comes out, and nothing throws.
    /// </summary>
    [Fact]
    public async Task ExeThatIsNotAnInstaller_YieldsNothing()
    {
        var exe = File("tool.exe");
        await exe.WriteAllBytesAsync("MZ"u8.ToArray().Concat(Enumerable.Repeat((byte) 0, 1022)).ToArray());

        var results = await Read(exe);

        Assert.Empty(results);
    }

    [Fact]
    public async Task ExtractAll_WritesWhatTheFilterAllows()
    {
        var archive = await TestArchives.Zip(File("mod.zip"), TestArchives.Entries);
        var dest = _folder.Path.Combine("installed");

        await _extractor.ExtractAll(archive, dest, CancellationToken.None,
            path => path.InFolder("meshes".ToRelativePath()));

        Assert.Equal("cuirass", await dest.Combine(TestArchives.Entries[0].Name).ReadAllTextAsync());
        Assert.False(dest.Combine("readme.txt").FileExists());
        Assert.False(dest.Combine("textures").DirectoryExists());
    }

    [Fact]
    public async Task ExtractAll_FromABsa_RecreatesItsFolders()
    {
        var archive = await TestArchives.Bsa(File("mod.bsa"), _manager, true, TestArchives.Entries);
        var dest = _folder.Path.Combine("installed");

        await _extractor.ExtractAll(archive, dest, CancellationToken.None);

        foreach (var (name, content) in TestArchives.Entries)
            Assert.Equal(content, await dest.Combine(name).ReadAllTextAsync());
    }
}
