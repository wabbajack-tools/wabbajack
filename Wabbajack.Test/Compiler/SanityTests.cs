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

namespace Wabbajack.Compiler.Test;

[ClassConstructor<CompilerClassConstructor>]
public class CompilerSanityTests
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
    private readonly IImageLoader _imageLoader;

    public CompilerSanityTests(ILogger<CompilerSanityTests> logger, IServiceProvider serviceProvider,
        FileExtractor.FileExtractor fileExtractor,
        TemporaryFileManager manager, 
        ParallelOptions parallelOptions,
        IImageLoader imageLoader)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _scope = _serviceProvider.CreateScope();
        _harness = _scope.ServiceProvider.GetService<ModListHarness>()!;
        _fileExtractor = fileExtractor;
        _manager = manager;
        _parallelOptions = parallelOptions;
        _imageLoader = imageLoader;
    }


    [Before(HookType.Test)]
    public async Task Setup()
    {
        _mod = await _harness.InstallMod(Ext.Zip,
            new Uri(
                "https://authored-files.wabbajack.org/Tonal%20Architect_WJ_TEST_FILES.zip_9cb97a01-3354-4077-9e4a-7e808d47794f"));
    }

    private async Task CompileAndValidate(int expectedDirectives, Action<CompilerSettings>? configureSettings = null)
    {
        _modlist = await _harness.Compile(configureSettings);
        await Assert.That(_modlist).IsNotNull();
        await Assert.That(_modlist!.Archives).HasSingleItem();

        await Assert.That(_modlist.Directives.Select(d => d.To).ToHashSet()).IsNotEmpty();
        await Assert.That(_modlist.Directives.Length).IsEqualTo(expectedDirectives);
    }

    private async Task InstallAndValidate()
    {
        await _harness.Install();

        foreach (var file in _mod.FullPath.EnumerateFiles())
            _harness.VerifyInstalledFile(file);
    }

    [Test]
    public async Task CanCompileDirectMatchFiles()
    {
        await CompileAndValidate(4);

        foreach (var directive in _modlist!.Directives.OfType<FromArchive>())
            await Assert.That(directive.ArchiveHashPath.Hash).IsEqualTo(_modlist.Archives.First().Hash);

        await InstallAndValidate();
    }

    [Test]
    public async Task CanPatchFiles()
    {
        foreach (var file in _mod.FullPath.EnumerateFiles(Ext.Esp))
        {
            await using var fs = file.Open(FileMode.Open, FileAccess.Write);
            fs.Position = 42;
            fs.WriteByte(42);
        }

        await CompileAndValidate(4);

        await Assert.That(_modlist!.Directives.OfType<PatchedFromArchive>()).HasSingleItem();
        await InstallAndValidate();
    }

    [Test]
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

    [Test]
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
        await Assert.That(_modlist!.Directives.OfType<CreateBSA>()).HasSingleItem();
        await InstallAndValidate();
    }

    [Test]
    public async Task DuplicateFilesAreCopied()
    {
        foreach (var file in _mod.FullPath.EnumerateFiles(Ext.Esp))
        {
            var newPath = file.RelativeTo(_mod.FullPath).RelativeTo(_mod.FullPath.Combine("duplicates"));
            newPath.Parent.CreateDirectory();
            await file.CopyToAsync(newPath, CancellationToken.None);
        }

        await CompileAndValidate(5);

        foreach (var directive in _modlist!.Directives.OfType<FromArchive>())
            await Assert.That(directive.ArchiveHashPath.Hash).IsEqualTo(_modlist.Archives.First().Hash);

        await InstallAndValidate();
    }

    [Test]
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

        await Assert.That(_modlist!.Directives.OfType<InlineFile>().Count()).IsEqualTo(3);

        await InstallAndValidate();
    }

    [Test]
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
            var oldState = await _imageLoader.Load(file);
            await Assert.That(oldState.Format).IsNotEqualTo(DXGI_FORMAT.UNKNOWN);
            _logger.LogInformation("Recompressing {file}", file.FileName);
            await _imageLoader.Recompress(file, 512, 512, 1, DXGI_FORMAT.BC7_UNORM, file, CancellationToken.None);

            var state = await _imageLoader.Load(file);
            await Assert.That(state.Format).IsEqualTo(DXGI_FORMAT.BC7_UNORM);
        }

        await CompileAndValidate(3);

        await Assert.That(_modlist!.Directives.OfType<TransformedTexture>().Count()).IsEqualTo(2);

        foreach (var directive in _modlist!.Directives.OfType<TransformedTexture>())
        {
            _logger.LogInformation("For file {name} {format}", directive.To.FileName, directive.ImageState.Format);
            await Assert.That(directive.ArchiveHashPath.Parts[^1].FileName).IsEqualTo(directive.To.FileName);
            await Assert.That(directive.ImageState.Height).IsEqualTo(512);
            await Assert.That(directive.ImageState.Width).IsEqualTo(512);
            await Assert.That(directive.ImageState.Format).IsEqualTo(DXGI_FORMAT.BC7_UNORM);
        }

        await InstallAndValidate();
    }
}