using System;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Wabbajack.CLI.Verbs;
using Wabbajack.Compiler;
using Wabbajack.Downloaders;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using Wabbajack.VFS;
using Xunit;

namespace Wabbajack.CLI.Test;

[Collection("CLI")]
public class CompilePublishTests : IDisposable
{
    private readonly AbsolutePath _tempDir;
    private readonly IServiceProvider _provider;

    public CompilePublishTests(CLITestFixture fixture)
    {
        _provider = fixture.ServiceProvider;
        _tempDir = Path.Combine(Path.GetTempPath(), "wj-pub-test-" + Guid.NewGuid().ToString("N")[..8])
            .ToAbsolutePath();
        _tempDir.CreateDirectory();
    }

    public void Dispose()
    {
        if (_tempDir.DirectoryExists())
        {
            try { _tempDir.DeleteDirectory(); }
            catch { /* best effort */ }
        }
    }

    [Fact]
    public void Definition_HasPublishFlag()
    {
        var publishOption = Compile.Definition.Options
            .FirstOrDefault(o => o.LongOption == "publish");

        Assert.NotNull(publishOption);
        Assert.Equal(typeof(bool), publishOption.Type);
    }

    [Fact]
    public async Task RunPublish_CallsPublisher_WithMetadataFromFile()
    {
        var fakeOutputFile = _tempDir.Combine("mylist.wabbajack".ToRelativePath());
        await fakeOutputFile.WriteAllBytesAsync(Array.Empty<byte>());

        var metadata = new DownloadMetadata
        {
            Size = 42,
            NumberOfArchives = 3,
            SizeOfArchives = 100,
            NumberOfInstalledFiles = 7,
            SizeOfInstalledFiles = 200
        };
        var dtos = _provider.GetRequiredService<DTOSerializer>();
        var metaPath = fakeOutputFile.WithExtension(new Extension(".meta")).WithExtension(new Extension(".json"));
        await using (var metaStream = metaPath.Open(FileMode.Create, FileAccess.Write))
        {
            await dtos.Serialize(metadata, metaStream);
        }

        var settings = new CompilerSettings
        {
            OutputFile = fakeOutputFile,
            MachineUrl = "myrepo/MyModlist",
            Version = Version.Parse("1.2.3.0")
        };

        var publisher = Substitute.For<IModlistPublisher>();
        IObservable<(Percent PercentDone, string Message)> emptyProgress =
            Observable.Empty<(Percent, string)>();
        publisher.PublishModlist(
                Arg.Any<string>(), Arg.Any<Version>(), Arg.Any<AbsolutePath>(), Arg.Any<DownloadMetadata>())
            .Returns(Task.FromResult((Progress: emptyProgress, PublishTask: Task.CompletedTask)));

        var compile = MakeCompile(publisher);

        var result = await compile.RunPublish(settings, CancellationToken.None);

        Assert.Equal(0, result);
        await publisher.Received(1).PublishModlist(
            "myrepo/MyModlist",
            Version.Parse("1.2.3.0"),
            fakeOutputFile,
            Arg.Is<DownloadMetadata>(m => m.Size == 42 && m.NumberOfArchives == 3));
    }

    [Fact]
    public async Task RunPublish_PublisherThrows_ExceptionPropagates()
    {
        var fakeOutputFile = _tempDir.Combine("mylist2.wabbajack".ToRelativePath());
        await fakeOutputFile.WriteAllBytesAsync(Array.Empty<byte>());

        var metadata = new DownloadMetadata { Size = 1 };
        var dtos = _provider.GetRequiredService<DTOSerializer>();
        var metaPath = fakeOutputFile.WithExtension(new Extension(".meta")).WithExtension(new Extension(".json"));
        await using (var metaStream = metaPath.Open(FileMode.Create, FileAccess.Write))
        {
            await dtos.Serialize(metadata, metaStream);
        }

        var settings = new CompilerSettings
        {
            OutputFile = fakeOutputFile,
            MachineUrl = "myrepo/MyModlist",
            Version = Version.Parse("1.0.0.0")
        };

        var publisher = Substitute.For<IModlistPublisher>();
        publisher.PublishModlist(
                Arg.Any<string>(), Arg.Any<Version>(), Arg.Any<AbsolutePath>(), Arg.Any<DownloadMetadata>())
            .Returns<Task<(IObservable<(Percent PercentDone, string Message)> Progress, Task PublishTask)>>(_ =>
                throw new InvalidOperationException("CDN auth failed"));

        var compile = MakeCompile(publisher);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => compile.RunPublish(settings, CancellationToken.None));
    }

    private Compile MakeCompile(IModlistPublisher publisher)
    {
        return new Compile(
            NullLogger<Compile>.Instance,
            publisher,
            _provider.GetRequiredService<DownloadDispatcher>(),
            _provider.GetRequiredService<DTOSerializer>(),
            _provider.GetRequiredService<FileHashCache>(),
            Substitute.For<IGameLocator>(),
            _provider,
            _provider.GetRequiredService<CompilerSettingsInferencer>());
    }
}
