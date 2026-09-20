using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.CLI.Builder;
using Wabbajack.Common;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Networking.Steam;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.CLI.Verbs;

/// <summary>
///     Reads a Steam depot manifest file by file and records what each file hashes to, into a clone of
///     <c>wabbajack-tools/indexed-game-files</c>.
///     <para>
///         This is the only way the hash of an old game file can be known. A manifest says a file's path,
///         its size and Valve's own SHA-1; a modlist says a file's xxHash64. Nothing connects the two but
///         the bytes, so somebody has to fetch a build once and write down what came out - after which a
///         repair can find any file of that build by hash, with no idea what the build was called.
///     </para>
///     <para>
///         Which manifests are worth indexing is the harder half, and Steam will not help: its client API
///         only ever says what a depot publishes now. So the ids come from one of three places, which is
///         what the options are for. <c>--installed</c> reads them out of the local <c>appmanifest</c>,
///         which is the build sitting on this disk and the only source for a version Steam has already
///         moved past. <c>--depot</c> with <c>--manifest</c> takes an id from somewhere else - the version
///         index, a note somebody made. With neither, it indexes whatever the game publishes today, which
///         is worth doing the day a build ships and worthless a year later.
///     </para>
///     <para>
///         It is safe to stop and safe to re-run: shards are written after every manifest, files already
///         recorded are skipped, and a manifest already read all the way through is skipped entirely unless
///         <c>--force</c> says otherwise. Bandwidth is what this costs - a Skyrim Special Edition build is
///         some fifteen gigabytes - and nothing is kept: each file is hashed into a temporary folder and
///         deleted.
///     </para>
/// </summary>
public class IndexSteamDepots
{
    private readonly ISteamContentClient _content;
    private readonly DTOSerializer _dtos;
    private readonly IGameLocator _locator;
    private readonly ILogger<IndexSteamDepots> _logger;
    private readonly ISteamSession _session;
    private readonly TemporaryFileManager _temp;

    public IndexSteamDepots(ILogger<IndexSteamDepots> logger, ISteamSession session, ISteamContentClient content,
        IGameLocator locator, TemporaryFileManager temp, DTOSerializer dtos)
    {
        _logger = logger;
        _session = session;
        _content = content;
        _locator = locator;
        _temp = temp;
        _dtos = dtos;
    }

    public static VerbDefinition Definition = new("index-steam-depots",
        "Hashes every file of a Steam depot manifest into the game file content index", new[]
        {
            new OptionDefinition(typeof(string), "g", "game", "WJ game whose depots to index"),
            new OptionDefinition(typeof(AbsolutePath), "o", "output",
                "Root of a clone of the indexed-game-files repo"),
            new OptionDefinition(typeof(uint), "a", "app",
                "Steam app id to read the depots under. Defaults to the game's own"),
            new OptionDefinition(typeof(uint), "d", "depot", "Index only this depot"),
            new OptionDefinition(typeof(ulong), "m", "manifest",
                "Index this manifest of --depot, instead of whatever it publishes now"),
            new OptionDefinition(typeof(bool), "i", "installed",
                "Index the build installed on this machine, from its Steam appmanifest"),
            new OptionDefinition(typeof(string), "v", "version",
                "Game version to record these manifests as. Defaults to the installed one with --installed"),
            new OptionDefinition(typeof(bool), "f", "force", "Re-read manifests that are already indexed")
        });

    public async Task<int> Run(string game, AbsolutePath output, uint app, uint depot, ulong manifest,
        bool installed, string version, bool force, CancellationToken token)
    {
        if (output == default)
        {
            _logger.LogError("--output is required: point it at a clone of the indexed-game-files repo");
            return 1;
        }

        GameMetaData meta;
        try
        {
            meta = GameRegistry.GetByFuzzyName(game);
        }
        catch (Exception)
        {
            _logger.LogError("No game matches \"{Game}\"", game);
            return 1;
        }

        var appId = app != 0 ? app : (uint) meta.SteamIDs.FirstOrDefault();
        if (appId == 0)
        {
            _logger.LogError("{Game} is not sold on Steam, so it has no depots to read", meta.Game);
            return 1;
        }

        try
        {
            await SteamVerbSupport.EnsureLoggedInAsync(_session, token);

            var targets = await Targets(meta.Game, appId, depot, manifest, installed, token);
            if (targets.Count == 0)
            {
                _logger.LogError("Nothing to index: no depot and manifest could be worked out for app {App}",
                    appId);
                return 1;
            }

            if (installed && string.IsNullOrWhiteSpace(version) &&
                _locator.TryGetSteamBuildId(meta.Game, out var buildId))
                version = buildId;

            var index = await GameFileIndexFolder.Load(output, meta.Game, _dtos, token);
            _logger.LogInformation("The index holds {Files} files of {Game} over {Manifests} manifests",
                index.FileCount, meta.Game, index.Manifests.Count);

            var indexed = 0;
            foreach (var target in targets)
            {
                token.ThrowIfCancellationRequested();
                indexed += await Index(index, meta.Game, appId, target, version, force, token);
            }

            _logger.LogInformation("Indexed {Files} new files of {Game} into {Output}", indexed, meta.Game,
                output.Combine(meta.Game.ToString()));
            return 0;
        }
        catch (Exception ex) when (SteamVerbSupport.Report(_logger, ex) is { } code)
        {
            return code;
        }
    }

    /// <summary>
    ///     Which (depot, manifest) pairs to read, in the order the options say. An explicit pair is taken as
    ///     given - it is the only way to reach a build nobody here has - the installed build comes from the
    ///     local appmanifest, and with neither it is whatever the app publishes today.
    /// </summary>
    private async Task<IReadOnlyList<DepotManifestId>> Targets(Game game, uint app, uint depot, ulong manifest,
        bool installed, CancellationToken token)
    {
        if (depot != 0 && manifest != 0)
            return new[] {new DepotManifestId(depot, manifest)};

        if (installed)
        {
            if (!_locator.TryGetSteamManifests(game, out var local))
            {
                _logger.LogError("{Game} does not appear to be installed through Steam on this machine", game);
                return Array.Empty<DepotManifestId>();
            }

            var fromInstall = local.Select(m => new DepotManifestId(m.Depot, m.Manifest)).ToArray();
            _logger.LogInformation("The installed copy of {Game} records {Count} depots", game,
                fromInstall.Length);
            return Restrict(fromInstall, depot);
        }

        var current = await _content.GetCurrentDepotsAsync(app);
        _logger.LogInformation("App {App} publishes {Count} depots on the public branch now", app, current.Count);
        return Restrict(current, depot);
    }

    private static IReadOnlyList<DepotManifestId> Restrict(IReadOnlyList<DepotManifestId> candidates, uint depot)
    {
        return depot == 0 ? candidates : candidates.Where(c => c.DepotId == depot).ToArray();
    }

    /// <summary>
    ///     Reads one manifest: every file fetched, hashed and thrown away, with what it hashed to written
    ///     down. Returns how many files it added.
    /// </summary>
    private async Task<int> Index(GameFileIndexFolder index, Game game, uint app, DepotManifestId target,
        string version, bool force, CancellationToken token)
    {
        if (!force && index.HasManifest(app, target.DepotId, target.ManifestId))
        {
            _logger.LogInformation("Depot {Depot} manifest {Manifest} is already indexed", target.DepotId,
                target.ManifestId);
            return 0;
        }

        await _content.EnsureAccessAsync(app, target.DepotId, token);

        var files = await _content.ListFilesAsync(app, target.DepotId, target.ManifestId, token);
        var total = files.Sum(f => (long) f.Size);
        _logger.LogInformation("Depot {Depot} manifest {Manifest} lists {Count} files ({Size})", target.DepotId,
            target.ManifestId, files.Count, total.ToFileSizeString());

        await using var scratch = _temp.CreateFolder();
        var added = 0;
        var done = 0L;

        foreach (var file in files.OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase))
        {
            token.ThrowIfCancellationRequested();
            done += (long) file.Size;

            if (index.Has(app, target.DepotId, target.ManifestId, file.Path)) continue;

            // One file at a time and deleted straight after: this is a hash, not a download, and a build
            // that would not fit on the disk still has to be indexable.
            var scratchFile = scratch.Path.Combine(RandomName.Next(12));
            try
            {
                await _content.DownloadFileAsync(app, target.DepotId, target.ManifestId, file.Path, scratchFile,
                    token);

                await using var stream = scratchFile.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
                var hash = await stream.HashingCopy(Stream.Null, token);

                index.Add(new IndexedGameFile
                {
                    Hash = hash,
                    Size = (long) file.Size,
                    Path = file.Path,
                    App = app,
                    Depot = target.DepotId,
                    Manifest = target.ManifestId
                });

                added++;
                if (added % 25 == 0)
                {
                    _logger.LogInformation("{Done} of {Total} ({Percent:P0}) - {File}",
                        done.ToFileSizeString(), total.ToFileSizeString(), total == 0 ? 1d : (double) done / total,
                        file.Path);

                    // Written as it goes, so stopping a run costs the file in flight and nothing else.
                    await index.Save(token);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One file that cannot be fetched is not a manifest that cannot be indexed - a depot with a
                // bad chunk in it has shipped before - so it is left out and the rest goes on. The manifest
                // is not recorded as complete below unless every file made it.
                _logger.LogWarning("Could not fetch {File} from depot {Depot}: {Message}", file.Path,
                    target.DepotId, ex.Message);
            }
            finally
            {
                if (scratchFile.FileExists()) scratchFile.Delete();
            }
        }

        var indexedNow = files.Count(f => index.Has(app, target.DepotId, target.ManifestId, f.Path));
        if (indexedNow == files.Count)
        {
            index.RecordManifest(new IndexedManifest
            {
                App = app,
                Depot = target.DepotId,
                Manifest = target.ManifestId,
                Version = version ?? string.Empty,
                Files = files.Count,
                Indexed = DateTime.UtcNow
            });
        }
        else
        {
            _logger.LogWarning(
                "Depot {Depot} manifest {Manifest} is {Indexed} of {Count} files, so it is not marked done; " +
                "run this again to finish it", target.DepotId, target.ManifestId, indexedNow, files.Count);
        }

        await index.Save(token);
        _logger.LogInformation("Depot {Depot} manifest {Manifest}: {Added} files added", target.DepotId,
            target.ManifestId, added);
        return added;
    }
}
