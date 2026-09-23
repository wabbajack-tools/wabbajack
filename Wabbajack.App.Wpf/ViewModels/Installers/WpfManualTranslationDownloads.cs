using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.Installer.Preflight;
using Wabbajack.Messages;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.Translation.Nexus;

namespace Wabbajack;

public class WpfManualTranslationDownloads : IManualTranslationDownloads
{
    private const string Instructions = "Press Manual Download on the Nexus Mods page, then Slow download";

    private readonly IServiceProvider _services;
    private readonly ILogger<WpfManualTranslationDownloads> _logger;

    public WpfManualTranslationDownloads(IServiceProvider services, ILogger<WpfManualTranslationDownloads> logger)
    {
        _services = services;
        _logger = logger;
    }

    public string LanguageName { get; set; } = string.Empty;

    public async Task<IReadOnlyDictionary<TranslationFile, AbsolutePath>> Acquire(IReadOnlyList<TranslationFile> files,
        AbsolutePath destination, CancellationToken token)
    {
        var byName = files.GroupBy(f => f.FileName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var archives = byName.Values.Select(f => new Archive
        {
            Name = f.FileName,
            Size = f.Size,
            State = new Nexus {Game = f.Game, ModID = f.ModId, FileID = f.FileId, Name = f.ModName}
        }).ToList();
        var queue = archives
            .Select(a => (a, new ManualDownloadTarget(new Uri(byName[a.Name].Url), "Nexus Mods", Instructions)))
            .ToList();

        await using var acquirer = _services.GetRequiredService<IManualDownloadAcquirer>();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        await acquirer.Start(archives, KnownFolders.Downloads, destination, cts.Token);

        using var downloads = new ManualDownloadsVM(acquirer, Observable.Return(new ManualQueueChanged(queue)),
            cts.Token, _logger);
        using var pane = new TranslationDownloadsVM(downloads, archives.Count, LanguageName);
        var shown = ShowTranslationDownloads.Send(pane);
        var completion = acquirer.WaitForCompletion(cts.Token);
        await Task.WhenAny(completion, shown, Task.Delay(Timeout.Infinite, token));
        pane.Close();
        token.ThrowIfCancellationRequested();

        var placed = acquirer.Snapshot()
            .Where(i => i.State == ManualDownloadState.Moved && i.PlacedPath != null && byName.ContainsKey(i.Key))
            .ToDictionary(i => byName[i.Key], i => i.PlacedPath!.Value);
        _logger.LogInformation("{Placed} of {Total} translation files were downloaded by hand", placed.Count,
            archives.Count);

        await cts.CancelAsync();
        try
        {
            await acquirer.Stop();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Stopping the translation download watcher");
        }

        return placed;
    }
}
