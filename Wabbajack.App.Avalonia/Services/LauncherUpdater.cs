using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.App.Avalonia.Util;
using Wabbajack.Common;
using Wabbajack.Downloaders;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.App.Avalonia.Services;

/// <summary>
///     The WPF app's LauncherUpdater, run once at startup. When the app is running from a launcher-laid-out
///     install (a version-named folder beside Wabbajack.exe), it deletes all but the two newest older versions
///     and replaces the launcher with the Wabbajack.exe attached to the newest GitHub release, if that is newer.
///     Run from anywhere else, as a development build is, it only logs that it is not updating.
///     <para>
///         The WPF version declared the release fields with Newtonsoft attributes but read them with
///         System.Text.Json, which ignores those, so every tag came back empty and the launcher was never
///         updated; only the cleanup of old versions ever ran. These use System.Text.Json's own names.
///     </para>
/// </summary>
public class LauncherUpdater
{
    private static readonly Uri GitHubReleases = new("https://api.github.com/repos/wabbajack-tools/wabbajack/releases");

    private readonly ILogger<LauncherUpdater> _logger;
    private readonly HttpClient _client;
    private readonly Client _wjClient;
    private readonly DownloadDispatcher _downloader;

    public LauncherUpdater(ILogger<LauncherUpdater> logger, HttpClient client, Client wjClient, DownloadDispatcher downloader)
    {
        _logger = logger;
        _client = client;
        _wjClient = wjClient;
        _downloader = downloader;
    }

    public async Task Run()
    {
        if (AppFolders.CommonFolder.Value == KnownFolders.EntryPoint)
        {
            _logger.LogInformation("Outside of standard install folder, not updating");
            return;
        }

        var version = Version.Parse(KnownFolders.EntryPoint.FileName.ToString());

        var oldVersions = AppFolders.CommonFolder.Value
            .EnumerateDirectories()
            .Select(f => Version.TryParse(f.FileName.ToString(), out var ver) ? (ver, f) : default)
            .Where(f => f != default)
            .Where(f => f.ver < version)
            .OrderByDescending(f => f)
            .Skip(2)
            .ToArray();

        foreach (var (_, path) in oldVersions)
        {
            _logger.LogInformation("Deleting old Wabbajack version at: {Path}", path);
            path.DeleteDirectory();
        }

        var release = (await GetReleases())
            .Select(r => Version.TryParse(r.Tag, out var ver) ? (version: ver, release: r) : default)
            .Where(r => r != default)
            .OrderByDescending(r => r.version)
            .Select(r =>
            {
                var asset = r.release.Assets.FirstOrDefault(a => a.Name == "Wabbajack.exe");
                return asset != default ? (r.version, r.release, asset) : default;
            })
            .FirstOrDefault();

        var launcherFolder = KnownFolders.EntryPoint.Parent;
        var exePath = launcherFolder.Combine("Wabbajack.exe");
        var launcherVersion = FileVersionInfo.GetVersionInfo(exePath.ToString());

        if (release == default || release.version <= Version.Parse(launcherVersion.FileVersion!))
            return;

        _logger.LogInformation("Updating Launcher from {OldVersion} to {NewVersion}", launcherVersion.FileVersion, release.version);
        var tempPath = launcherFolder.Combine("Wabbajack.exe.temp");

        try
        {
            await _downloader.Download(new Archive
            {
                State = new Http { Url = release.asset.BrowserDownloadUrl! },
                Name = release.asset.Name,
                Size = release.asset.Size
            }, tempPath, CancellationToken.None);
        }
        catch (Exception ex)
        {
            // A stalled download throws rather than reporting an empty hash; the next start tries again.
            _logger.LogWarning(ex, "Could not download the new launcher, keeping the current one");
            return;
        }

        if (tempPath.Size() != release.asset.Size)
        {
            _logger.LogInformation("Downloaded launcher did not match expected size: {DownloadedSize} expected {ExpectedSize}",
                tempPath.Size(), release.asset.Size);
            return;
        }

        if (exePath.FileExists())
            exePath.Delete();
        await tempPath.MoveToAsync(exePath, true, CancellationToken.None);

        _logger.LogInformation("Finished updating wabbajack");
        await _wjClient.SendMetric("updated_launcher", $"{launcherVersion.FileVersion} -> {release.version}");
    }

    private async Task<Release[]> GetReleases()
    {
        _logger.LogInformation("Getting new Wabbajack version list");
        using var msg = new HttpRequestMessage(HttpMethod.Get, GitHubReleases);
        msg.AddChromeAgent();
        using var response = await _client.SendAsync(msg);
        response.EnsureSuccessStatusCode();
        return await JsonSerializer.DeserializeAsync<Release[]>(await response.Content.ReadAsStreamAsync()) ?? [];
    }

    public class Release
    {
        [JsonPropertyName("tag_name")] public string Tag { get; set; } = "";
        [JsonPropertyName("assets")] public Asset[] Assets { get; set; } = [];
    }

    public class Asset
    {
        [JsonPropertyName("browser_download_url")] public Uri? BrowserDownloadUrl { get; set; }
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("size")] public long Size { get; set; }
    }
}
