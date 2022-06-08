using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wabbajack.Common;
using Wabbajack.Compression.BSA;
using Wabbajack.DTOs;
using Wabbajack.DTOs.Directives;
using Wabbajack.DTOs.Texture;
using Wabbajack.Hashing.PHash;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using Wabbajack.Services.OSIntegrated;
using Xunit;

namespace Wabbajack.Compiler.Test;

public class CompilerSanityTests : IAsyncLifetime
{
    private readonly FileExtractor.FileExtractor _fileExtractor;
    private readonly ModListHarness _harness;
    private readonly ILogger<CompilerSanityTests> _logger;
    private readonly TemporaryFileManager _manager;
    private readonly ParallelOptions _parallelOptions;
    private readonly IServiceScope _scope;
    private readonly IServiceProvider _serviceProvider;
    private Mod _mod;
    private ModList? _modlist;
    private readonly LoggingRateLimiterReporter _reporter;

    public CompilerSanityTests(ILogger<CompilerSanityTests> logger, IServiceProvider serviceProvider,
        FileExtractor.FileExtractor fileExtractor,
        TemporaryFileManager manager, ParallelOptions parallelOptions)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _scope = _serviceProvider.CreateScope();
        _harness = _scope.ServiceProvider.GetService<ModListHarness>()!;
        _fileExtractor = fileExtractor;
        _manager = manager;
        _parallelOptions = parallelOptions;
    }


    public async Task InitializeAsync()
    {
        _mod = await _harness.InstallMod(Ext.Zip,
            new Uri(
                "https://authored-files.wabbajack.org/Tonal%20Architect_WJ_TEST_FILES.zip_9cb97a01-3354-4077-9e4a-7e808d47794f"));
    }

    public async Task DisposeAsync()
    {
    }

    private async Task CompileAndValidate(int expectedDirectives, Action<CompilerSettings>? configureSettings = null)
    {
        _modlist = await _harness.Compile(configureSettings);
        Assert.NotNull(_modlist);
        Assert.Single(_modlist!.Archives);

        Assert.NotEmpty(_modlist.Directives.Select(d => d.To).ToHashSet());
        Assert.Equal(expectedDirectives, _modlist.Directives.Length);
    }

    private async Task InstallAndValidate()
    {
        await _harness.Install();

        foreach (var file in _mod.FullPath.EnumerateFiles())
            _harness.VerifyInstalledFile(file);
    }

    [Fact]
    public async Task CanCompileDirectMatchFiles()
    {
        await CompileAndValidate(4);

        foreach (var directive in _modlist!.Directives.OfType<FromArchive>())
            Assert.Equal(_modlist.Archives.First().Hash, directive.ArchiveHashPath.Hash);

        await InstallAndValidate();
    }

    [Fact]
    public async Task CanPatchFiles()
    {
        foreach (var file in _mod.FullPath.EnumerateFiles(Ext.Esp))
        {
            await using var fs = file.Open(FileMode.Open, FileAccess.Write);
            fs.Position = 42;
            fs.WriteByte(42);
        }

        await CompileAndValidate(4);

        Assert.Single(_modlist.Directives.OfType<PatchedFromArchive>());
        await InstallAndValidate();
    }

    [Fact]
    public async Task CanExtractBSAs()
    {
        var bsa = _mod.FullPath.EnumerateFiles(Ext.Bsa)
            .OrderBy(d => d.Size())
            .First();
        await _fileExtractor.ExtractAll(bsa, _mod.FullPath, CancellationToken.None);
        bsa.Delete();

        await CompileAndValidate(39);
        await InstallAndValidate();
    }

    [Fact]
    public async Task CanRecreateBSAs()
    {
        var bsa = _mod.FullPath.EnumerateFiles(Ext.Bsa).MinBy(d => d.Size());
        await _fileExtractor.ExtractAll(bsa, _mod.FullPath, CancellationToken.None);

        var reader = await BSADispatch.Open(bsa);
        var bsaState = reader.State;
        var fileStates = reader.Files.Select(f => f.State).ToArray();
        bsa.Delete();

        await using var creator = BSADispatch.CreateBuilder(bsaState, _manager);
        await fileStates.Take(2).PDoAll(new Resource<CompilerSanityTests>(),
            async f => await creator.AddFile(f, f.Path.RelativeTo(_mod.FullPath).Open(FileMode.Open),
                CancellationToken.None));
        {
            await using var fs = bsa.Open(FileMode.Create, FileAccess.Write);
            await creator.Build(fs, CancellationToken.None);
        }

        await CompileAndValidate(42);
        Assert.Single(_modlist.Directives.OfType<CreateBSA>());
        await InstallAndValidate();
    }

    [Fact]
    public async Task DuplicateFilesAreCopied()
    {
        foreach (var file in _mod.FullPath.EnumerateFiles(Ext.Esp))
        {
            var newPath = file.RelativeTo(_mod.FullPath).RelativeTo(_mod.FullPath.Combine("duplicates"));
            newPath.Parent.CreateDirectory();
            await file.CopyToAsync(newPath, true, CancellationToken.None);
        }

        await CompileAndValidate(5);

        foreach (var directive in _modlist!.Directives.OfType<FromArchive>())
            Assert.Equal(_modlist.Archives.First().Hash, directive.ArchiveHashPath.Hash);

        await InstallAndValidate();
    }

    [Fact]
    public async Task NoMatchIncludeIncludesNonMatchingFiles()
    {
        var someFile = _mod.FullPath.Combine("some folder", "some file.pex");
        someFile.Parent.CreateDirectory();
        await someFile.WriteAllTextAsync("Cheese for Everyone!");

        var someFile2 = _mod.FullPath.Combine("some folder2", "some other folder", "some file.pex");
        someFile2.Parent.CreateDirectory();
        await someFile2.WriteAllTextAsync("More Cheese for Everyone!");

        await CompileAndValidate(6, settings =>
        {
            settings.NoMatchInclude = new[]
            {
                someFile.RelativeTo(_harness._source),
                someFile2.RelativeTo(_harness._source)
            };
        });

        Assert.Equal(3, _modlist!.Directives.OfType<InlineFile>().Count());

        await InstallAndValidate();
    }

    [Fact]
    public async Task CanDetectSimilarUnpackedTextures()
    {
        foreach (var bsa in _mod.FullPath.EnumerateFiles(Ext.Bsa))
        {
            await _fileExtractor.ExtractAll(bsa, _mod.FullPath, CancellationToken.None, p => p.Extension == Ext.Dds);
            bsa.Delete();
        }

        foreach (var file in _mod.FullPath.EnumerateFiles()
            .Where(p => p.Extension != Ext.Dds || !p.FileName.FileNameStartsWith("mrkinn"))) file.Delete();

        foreach (var file in _mod.FullPath.EnumerateFiles())
        {
            var oldState = await ImageLoader.Load(file);
            Assert.NotEqual(DXGI_FORMAT.UNKNOWN, oldState.Format);
            _logger.LogInformation("Recompressing {file}", file.FileName);
            await ImageLoader.Recompress(file, 512, 512, DXGI_FORMAT.BC7_UNORM, file, CancellationToken.None);

            var state = await ImageLoader.Load(file);
            Assert.Equal(DXGI_FORMAT.BC7_UNORM, state.Format);
        }

        await CompileAndValidate(3);

        Assert.Equal(2, _modlist!.Directives.OfType<TransformedTexture>().Count());

        foreach (var directive in _modlist!.Directives.OfType<TransformedTexture>())
        {
            _logger.LogInformation("For file {name} {format}", directive.To.FileName, directive.ImageState.Format);
            Assert.Equal(directive.To.FileName, directive.ArchiveHashPath.Parts[^1].FileName);
            Assert.Equal(512, directive.ImageState.Height);
            Assert.Equal(512, directive.ImageState.Width);
            Assert.Equal(DXGI_FORMAT.BC7_UNORM, directive.ImageState.Format);
        }

        await InstallAndValidate();
    }
}