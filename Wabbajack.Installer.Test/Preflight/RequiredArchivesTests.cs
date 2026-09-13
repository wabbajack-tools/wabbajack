#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wabbajack.Downloaders;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.DTOs.Directives;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Hashing.PHash;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Installer.Preflight.Rules;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using Wabbajack.VFS;
using Xunit;

namespace Wabbajack.Installer.Test.Preflight;

/// <summary>
///     <c>RequiredArchives.Compute</c> is the dry run of the pruning <c>AInstaller.OptimizeModlist</c> does
///     for real. The last test runs both on the same install and demands the same answer, so the two cannot
///     drift apart without a test noticing.
/// </summary>
public class RequiredArchivesTests : IDisposable
{
    private readonly PreflightTestHost _host;
    private readonly IServiceProvider _provider;

    public RequiredArchivesTests(IServiceProvider provider)
    {
        _provider = provider;
        _host = new PreflightTestHost(provider);
    }

    public void Dispose()
    {
        _host.Dispose();
    }

    private AbsolutePath Install => _host.Config.Install;

    /// <summary>
    ///     Three archives: A feeds a plain file and a file inside a BSA that gets built; B feeds another plain
    ///     file; C feeds a file that gets written into the same BSA as A's.
    /// </summary>
    private async Task<(Archive A, Archive B, Archive C, CreateBSA Bsa)> BuildModList()
    {
        var a = await PreflightTestHost.ArchiveFor("a.7z", "archive a");
        var b = await PreflightTestHost.ArchiveFor("b.7z", "archive b!");
        var c = await PreflightTestHost.ArchiveFor("c.7z", "archive c!!");

        var bsa = new CreateBSA
        {
            To = "mods/built/built.bsa".ToRelativePath(),
            TempID = "bsa-1".ToRelativePath(),
            Hash = await PreflightTestHost.HashOf("built bsa bytes"),
            Size = 15
        };

        async Task<FromArchive> From(Archive archive, string to, string content)
        {
            return new FromArchive
            {
                To = to.ToRelativePath(),
                Hash = await PreflightTestHost.HashOf(content),
                Size = content.Length,
                ArchiveHashPath = new HashRelativePath(archive.Hash, "inner.txt".ToRelativePath())
            };
        }

        _host.Config.ModList.Archives = new[] {a, b, c};
        _host.Config.ModList.Directives = new Directive[]
        {
            await From(a, "mods/a/plain.txt", "plain from a"),
            await From(b, "mods/b/plain.txt", "plain from b"),
            await From(a, $"{Consts.BSACreationDir}/bsa-1/from-a.nif", "bsa part from a"),
            await From(c, $"{Consts.BSACreationDir}/bsa-1/from-c.nif", "bsa part from c"),
            bsa
        };
        return (a, b, c, bsa);
    }

    private Task<Archive[]> Compute()
    {
        return RequiredArchives.Compute(_host.Config.ModList, Install, _host.Cache, _host.Limiter,
            CancellationToken.None);
    }

    [Fact]
    public async Task AFreshInstallNeedsEveryArchive()
    {
        await BuildModList();
        var required = await Compute();
        Assert.Equal(new[] {"a.7z", "b.7z", "c.7z"}, required.Select(r => r.Name).OrderBy(n => n));
    }

    [Fact]
    public async Task AnAlreadyInstalledDirectiveDropsItsArchive()
    {
        await BuildModList();
        await PreflightTestHost.WriteFile(Install.Combine("mods/b/plain.txt"), "plain from b");

        var required = await Compute();

        Assert.Equal(new[] {"a.7z", "c.7z"}, required.Select(r => r.Name).OrderBy(n => n));
    }

    [Fact]
    public async Task AnInstalledFileWithTheWrongContentStillNeedsItsArchive()
    {
        await BuildModList();
        await PreflightTestHost.WriteFile(Install.Combine("mods/b/plain.txt"), "plain from b, edited");

        var required = await Compute();

        Assert.Contains(required, r => r.Name == "b.7z");
    }

    [Fact]
    public async Task ABuiltBsaDropsTheArchivesThatOnlyFeedIt()
    {
        var (_, _, _, bsa) = await BuildModList();
        await PreflightTestHost.WriteFile(Install.Combine(bsa.To), "built bsa bytes");

        var required = await Compute();

        // A still feeds a plain file; C only fed the BSA.
        Assert.Equal(new[] {"a.7z", "b.7z"}, required.Select(r => r.Name).OrderBy(n => n));
    }

    [Fact]
    public async Task ComputeChangesNothingOnDisk()
    {
        await BuildModList();
        await PreflightTestHost.WriteFile(Install.Combine("mods/b/plain.txt"), "plain from b");
        await PreflightTestHost.WriteFile(Install.Combine("mods/stray/leftover.txt"), "not in the list");
        var before = Install.EnumerateFiles().OrderBy(f => f.ToString()).ToArray();
        var archives = _host.Config.ModList.Archives;
        var directives = _host.Config.ModList.Directives;

        await Compute();

        Assert.Equal(before, Install.EnumerateFiles().OrderBy(f => f.ToString()).ToArray());
        Assert.Same(archives, _host.Config.ModList.Archives);
        Assert.Same(directives, _host.Config.ModList.Directives);
    }

    [Fact]
    public async Task MatchesOptimizeModlistPruning()
    {
        var (_, _, _, bsa) = await BuildModList();
        // An update over a partial install: one plain file is already right, one is wrong, the BSA is built,
        // and a stray file is lying around for the installer to clean up.
        await PreflightTestHost.WriteFile(Install.Combine("mods/b/plain.txt"), "plain from b");
        await PreflightTestHost.WriteFile(Install.Combine("mods/a/plain.txt"), "plain from a, edited");
        await PreflightTestHost.WriteFile(Install.Combine(bsa.To), "built bsa bytes");
        await PreflightTestHost.WriteFile(Install.Combine("mods/stray/leftover.txt"), "not in the list");

        var dryRun = (await Compute()).Select(a => a.Name).OrderBy(n => n).ToArray();

        using var scope = _provider.CreateScope();
        var installer = new OptimizeOnlyInstaller(scope.ServiceProvider, _host);
        Assert.True(await installer.Optimize(CancellationToken.None));
        var real = _host.Config.ModList.Archives.Select(a => a.Name).OrderBy(n => n).ToArray();

        Assert.Equal(new[] {"a.7z"}, dryRun);
        Assert.Equal(dryRun, real);
        Assert.Equal(new[] {"mods/a/plain.txt"},
            _host.Config.ModList.Directives.Select(d => d.To.ToString().Replace('\\', '/')));
    }

    private sealed class OptimizeOnlyInstaller : AInstaller<OptimizeOnlyInstaller>
    {
        public OptimizeOnlyInstaller(IServiceProvider sp, PreflightTestHost host) : base(
            sp.GetRequiredService<ILogger<OptimizeOnlyInstaller>>(), host.Config, host.Locator,
            sp.GetRequiredService<FileExtractor.FileExtractor>(), sp.GetRequiredService<DTOSerializer>(),
            sp.GetRequiredService<Context>(), host.Cache, sp.GetRequiredService<DownloadDispatcher>(),
            sp.GetRequiredService<ParallelOptions>(), host.Limiter, sp.GetRequiredService<Client>(),
            sp.GetRequiredService<IImageLoader>())
        {
        }

        public override Task<InstallResult> Begin(CancellationToken token)
        {
            throw new NotSupportedException();
        }

        public Task<bool> Optimize(CancellationToken token)
        {
            return OptimizeModlist(token);
        }
    }
}
