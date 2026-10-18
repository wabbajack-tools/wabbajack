using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.App.Avalonia.Util;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.App.Avalonia.Services;

/// <summary>
///     Run once at startup when the app runs from a launcher-laid-out install (a version-named folder beside the
///     launcher). It deletes all but the two newest older versions, and brings the launcher up to date: the launcher
///     updates the app, and the app updates the launcher.
///     <para>
///         The launcher is compared with the newest release's by content, through the SHA-256 digest GitHub
///         publishes for every release asset, because launchers carry no Wabbajack version to compare. The WPF app
///         compared file versions and read the release list with Newtonsoft attributes through System.Text.Json, so
///         from 3.0 on no tag ever parsed and no launcher was replaced; 2.x was the last that updated it.
///     </para>
/// </summary>
public class LauncherUpdater(ILogger<LauncherUpdater> logger, HttpClient http, Client wjClient)
{
    // WABBAJACK_LAUNCHER_RELEASES, the launcher's own, points this at another release list for testing.
    private static readonly Uri Releases = new(
        Environment.GetEnvironmentVariable("WABBAJACK_LAUNCHER_RELEASES") is {Length: > 0} url
            ? url
            : "https://api.github.com/repos/wabbajack-tools/wabbajack/releases");

    /// <summary>Where the digest of the launcher asset last installed is kept, for a launcher that is not the asset.</summary>
    private const string InstalledDigestFile = ".launcher-sha256";

    public async Task Run()
    {
        if (AppFolders.CommonFolder.Value == KnownFolders.EntryPoint)
        {
            logger.LogInformation("Outside of standard install folder, not updating");
            return;
        }

        DeleteOldVersions();
        await UpdateLauncher(CancellationToken.None);
    }

    private void DeleteOldVersions()
    {
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
            logger.LogInformation("Deleting old Wabbajack version at: {Path}", path);
            path.DeleteDirectory();
        }
    }

    private async Task UpdateLauncher(CancellationToken token)
    {
        var desktop = Desktop.Current;
        var folder = AppFolders.CommonFolder.Value;
        var launcher = folder.Combine(PlatformExecutable.Name("Wabbajack"));
        var installedDigestFile = folder.Combine(InstalledDigestFile);

        using var request = new HttpRequestMessage(HttpMethod.Get, Releases);
        request.Headers.UserAgent.ParseAdd("Wabbajack");
        using var response = await http.SendAsync(request, token);
        response.EnsureSuccessStatusCode();
        var releases = await JsonSerializer.DeserializeAsync<Release[]>(
            await response.Content.ReadAsStreamAsync(token), cancellationToken: token) ?? [];

        if (LauncherReleases.Newest(releases.Select(r => r.ToInfo()), desktop.LauncherAssetName) is not { } newest)
        {
            logger.LogInformation("No release carries a launcher with a digest to compare, not updating it");
            return;
        }

        var current = desktop.LauncherAssetIsTheLauncher
            ? launcher.FileExists() ? await Sha256(launcher) : null
            : installedDigestFile.FileExists() ? (await installedDigestFile.ReadAllTextAsync()).Trim() : null;
        if (current == newest.Sha256)
        {
            logger.LogInformation("The launcher is the one in {Version}, up to date", newest.Version);
            return;
        }

        logger.LogInformation("Updating the launcher to the one in {Version}", newest.Version);
        var download = folder.Combine(PlatformExecutable.Name("Wabbajack") + ".download");
        try
        {
            await using (var source = await http.GetStreamAsync(newest.Url, token))
            await using (var file = File.Create(download.ToString()))
                await source.CopyToAsync(file, token);

            if (await Sha256(download) != newest.Sha256)
            {
                logger.LogWarning("The downloaded launcher does not match the release's digest, keeping the current one");
                return;
            }

            await desktop.InstallLauncher(download, launcher, token);
            await installedDigestFile.WriteAllTextAsync(newest.Sha256, token);
            logger.LogInformation("Updated the launcher to the one in {Version}", newest.Version);
            await wjClient.SendMetric("updated_launcher", newest.Version.ToString());
        }
        finally
        {
            if (download.FileExists()) download.Delete();
        }
    }

    private static async Task<string> Sha256(AbsolutePath file)
    {
        await using var stream = File.OpenRead(file.ToString());
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream));
    }

    private sealed class Release
    {
        [JsonPropertyName("tag_name")] public string Tag { get; set; } = "";
        [JsonPropertyName("prerelease")] public bool Prerelease { get; set; }
        [JsonPropertyName("assets")] public Asset[] Assets { get; set; } = [];

        public LauncherReleases.Release ToInfo() => new(Tag, Prerelease,
            Assets.Select(a => new LauncherReleases.Asset(a.Name, a.Url, a.Digest)).ToList());
    }

    private sealed class Asset
    {
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("browser_download_url")] public Uri? Url { get; set; }
        [JsonPropertyName("digest")] public string? Digest { get; set; }
    }
}

/// <summary>Which release's launcher the app installs.</summary>
public static class LauncherReleases
{
    public record Release(string Tag, bool Prerelease, IReadOnlyList<Asset> Assets);

    public record Asset(string Name, Uri? Url, string? Digest);

    public record Launcher(Version Version, Uri Url, string Sha256);

    /// <summary>
    ///     The launcher asset of the newest stable release that has one with a SHA-256 digest: tags of four numbers
    ///     only, as the launchers themselves read them, so a beta's launcher is never installed.
    /// </summary>
    public static Launcher? Newest(IEnumerable<Release> releases, string assetName)
    {
        return releases
            .Where(r => !r.Prerelease && r.Tag.Split('.').Length == 4 && Version.TryParse(r.Tag, out _))
            .Select(r => (Version: Version.Parse(r.Tag),
                Asset: r.Assets.FirstOrDefault(a => a.Name == assetName && a.Url != null &&
                                                    a.Digest?.StartsWith("sha256:") == true)))
            .Where(r => r.Asset != null)
            .OrderByDescending(r => r.Version)
            .Select(r => new Launcher(r.Version, r.Asset!.Url!, r.Asset.Digest!["sha256:".Length..].ToLowerInvariant()))
            .FirstOrDefault();
    }
}
