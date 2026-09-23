using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Wabbajack.Downloaders;
using Wabbajack.Networking.Steam;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;

namespace Wabbajack.Translation.Steam;

public sealed record VoiceDownloadResult(IReadOnlyList<string> Archives, IReadOnlyList<string> Failed);

public sealed class VoiceDownloader
{
    private readonly ISteamContentClient _content;
    private readonly ISteamSession _session;
    private readonly IResource<DownloadDispatcher> _downloads;
    private readonly ILogger<VoiceDownloader> _logger;

    public VoiceDownloader(ISteamContentClient content, ISteamSession session, IResource<DownloadDispatcher> downloads,
        ILogger<VoiceDownloader> logger)
    {
        _downloads = downloads;
        _content = content;
        _session = session;
        _logger = logger;
    }

    public static string? GameVersion(AbsolutePath gameFolder, string executable)
    {
        var exe = gameFolder.Combine(executable);
        if (!exe.FileExists()) return null;
        var info = FileVersionInfo.GetVersionInfo(exe.ToString());
        return $"{info.FileMajorPart}.{info.FileMinorPart}.{info.FileBuildPart}.{info.FilePrivatePart}";
    }

    public async Task<VoiceDownloadResult> Download(Mo2Instance instance, string code, AbsolutePath outputMod,
        Action<string, int, int>? progress, CancellationToken token)
    {
        if (!_session.IsLoggedIn)
            await _session.LoginWithStoredTokenAsync(token);

        var game = instance.Support;
        var version = GameVersion(instance.GameFolder, game.Executable);
        var wanted = game.SteamVoices
            .Where(d => d.Depots.ContainsKey(code) && instance.DataFolder.Combine(d.Plugin).FileExists())
            .ToList();

        var archives = new List<string>();
        var failed = new List<string>();
        var done = 0;
        foreach (var depot in wanted)
        {
            token.ThrowIfCancellationRequested();
            var archive = string.Format(depot.ArchiveFormat, code);
            progress?.Invoke(archive, done++, wanted.Count);
            var depotId = depot.Depots[code];
            try
            {
                // Pinned manifests match older game builds. Otherwise get the current one
                var manifest = version != null && depot.ManifestsByGameVersion.TryGetValue(version, out var pinned) &&
                               pinned.TryGetValue(code, out var id)
                    ? id
                    : await _content.GetCurrentManifestIdAsync(game.SteamAppId, depotId) ??
                      throw new InvalidOperationException($"Steam publishes no manifest for depot {depotId}");

                var output = outputMod.Combine(archive);
                output.Parent.CreateDirectory();
                using var job = await _downloads.Begin($"Downloading {archive} from Steam", 0, token);
                await _content.DownloadFileAsync(game.SteamAppId, depotId, manifest, "Data\\" + archive, output, token,
                    job);
                archives.Add(archive);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning("Could not download {Archive} from Steam depot {Depot}: {Message}", archive, depotId,
                    ex.Message);
                failed.Add(archive);
            }
        }

        return new VoiceDownloadResult(archives, failed);
    }
}
