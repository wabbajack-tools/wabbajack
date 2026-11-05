#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wabbajack.Downloaders;
using Wabbajack.DTOs;
using Wabbajack.DTOs.Directives;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Hashing.PHash;
using Wabbajack.Installer.Preflight.Rules;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using Wabbajack.VFS;
using Xunit;

namespace Wabbajack.Installer.Test.Preflight;

public class InstallFolderConfirmationTests : IDisposable
{
    private readonly IServiceProvider _provider;
    private readonly PreflightTestHost _host;

    public InstallFolderConfirmationTests(IServiceProvider provider)
    {
        _provider = provider;
        _host = new PreflightTestHost(provider);
    }

    public void Dispose()
    {
        _host.Dispose();
    }

    private async Task<CountingInstaller> Run(bool answer = true)
    {
        using var scope = _provider.CreateScope();
        var installer = new CountingInstaller(scope.ServiceProvider, _host, answer);
        installer.Result = await installer.Optimize(CancellationToken.None);
        return installer;
    }

    private async Task AFolderHoldingAnotherModList()
    {
        _host.Config.ModList.Directives = new Directive[] {new InlineFile {To = "mods/a/plain.txt".ToRelativePath()}};
        await PreflightTestHost.WriteFile(_host.Config.Install.Combine("ModOrganizer.ini"), "[General]");
        await PreflightTestHost.WriteFile(_host.Config.Install.Combine("portable.txt"), "");
        await PreflightTestHost.WriteFile(_host.Config.Install.Combine("mods", "theirs", "stale.txt"), "stale");
    }

    [Fact]
    public async Task WithoutAPreflightVerdict_TheUserIsAsked()
    {
        await AFolderHoldingAnotherModList();

        var installer = await Run();

        Assert.Equal(1, installer.ConfirmationsAsked);
        Assert.Null(installer.Result);
    }

    [Fact]
    public async Task WhenPreflightAlreadyAskedTheSameQuestion_TheUserIsNotAskedTwice()
    {
        await AFolderHoldingAnotherModList();
        _host.Config.PreflightInstallFolder =
            new InstallFolderVerdict(InstallFolderKind.ModListInstall, "", null, false);

        var installer = await Run();

        Assert.Equal(0, installer.ConfirmationsAsked);
        Assert.Null(installer.Result);
    }

    [Fact]
    public async Task APreflightVerdictForAnUpdate_DoesNotSuppressADifferentQuestion()
    {
        await AFolderHoldingAnotherModList();
        _host.Config.PreflightInstallFolder =
            new InstallFolderVerdict(InstallFolderKind.ModListInstall, "", null, true);

        var installer = await Run();

        Assert.Equal(1, installer.ConfirmationsAsked);
    }

    [Fact]
    public async Task DecliningStopsTheInstall()
    {
        await AFolderHoldingAnotherModList();

        var installer = await Run(false);

        Assert.Equal(InstallResult.Cancelled, installer.Result);
    }

    [Fact]
    public async Task AFolderOfUnrelatedFilesIsRefusedWithoutAsking()
    {
        _host.Config.ModList.Directives = new Directive[] {new InlineFile {To = "mods/a/plain.txt".ToRelativePath()}};
        await PreflightTestHost.WriteFile(_host.Config.Install.Combine("taxes.xlsx"), "mine");

        var installer = await Run();

        Assert.Equal(0, installer.ConfirmationsAsked);
        Assert.Equal(InstallResult.UnsafeInstallFolder, installer.Result);
    }

    [Fact]
    public async Task AnUpToDateInstallOfTheSameListIsAnUpdate()
    {
        var directives = new List<Directive>();
        for (var i = 0; i < 60; i++)
        {
            var to = $"mods/a/file{i}.txt";
            directives.Add(new InlineFile {To = to.ToRelativePath()});
            await PreflightTestHost.WriteFile(_host.Config.Install.Combine(to.ToRelativePath()), "x");
        }

        _host.Config.ModList.Directives = directives.ToArray();
        await PreflightTestHost.WriteFile(_host.Config.Install.Combine("mods", "a", "leftover.txt"), "stale");

        var installer = await Run();

        Assert.Equal(1, installer.ConfirmationsAsked);
        Assert.Contains("is already installed here", installer.LastMessage);
        Assert.False(installer.LastSevere);
    }

    private sealed class CountingInstaller : AInstaller<CountingInstaller>
    {
        public CountingInstaller(IServiceProvider sp, PreflightTestHost host, bool answer) : base(
            sp.GetRequiredService<ILogger<CountingInstaller>>(), host.Config, host.Locator,
            sp.GetRequiredService<FileExtractor.FileExtractor>(), sp.GetRequiredService<DTOSerializer>(),
            sp.GetRequiredService<Context>(), host.Cache, sp.GetRequiredService<DownloadDispatcher>(),
            sp.GetRequiredService<ParallelOptions>(), host.Limiter, sp.GetRequiredService<Client>(),
            sp.GetRequiredService<IImageLoader>())
        {
            OnConfirmAction = request =>
            {
                ConfirmationsAsked++;
                LastMessage = request.Message;
                LastSevere = request.Severe;
                return Task.FromResult(answer);
            };
        }

        public int ConfirmationsAsked { get; private set; }
        public string LastMessage { get; private set; } = "";
        public bool LastSevere { get; private set; }
        public InstallResult? Result { get; set; }

        public override Task<InstallResult> Begin(CancellationToken token)
        {
            throw new NotSupportedException();
        }

        public Task<InstallResult?> Optimize(CancellationToken token) => OptimizeModlist(token);
    }
}
