using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Wabbajack.Common;
using Wabbajack.Lib.Http;

namespace Wabbajack.Lib
{
    public class LauncherUpdater
    {
        public static Lazy<AbsolutePath> CommonFolder = new (() =>
        {
            var entryPoint = AbsolutePath.EntryPoint;

            // If we're not in a folder that looks like a version, abort
            if (!Version.TryParse(entryPoint.FileName.ToString(), out var version))
            {
                return entryPoint;
            }

            // If we're not in a folder that has Wabbajack.exe in the parent folder, abort
            if (!entryPoint.Parent.Combine(Consts.AppName).WithExtension(new Extension(".exe")).IsFile)
            {
                return entryPoint;
            }

            return entryPoint.Parent;
        });
        
        public static async Task Run()
        {

            if (CommonFolder.Value == AbsolutePath.EntryPoint)
            {
                Utils.Log("Outside of standard install folder, not updating");
                return;
            }

            var version = Version.Parse(AbsolutePath.EntryPoint.FileName.ToString());

            var oldVersions = CommonFolder.Value
                .EnumerateDirectories()
                .Select(f => Version.TryParse(f.FileName.ToString(), out var ver) ? (ver, f) : default)
                .Where(f => f != default)
                .Where(f => f.ver < version)
                .Select(f => f!)
                .OrderByDescending(f => f)
                .Skip(2)
                .ToArray();

            foreach (var (_, path) in oldVersions)
            {
                Utils.Log($"Deleting old Wabbajack version at: {path}");
                await path.DeleteDirectory();
            }

            var release = (await GetReleases())
                .Select(release => Version.TryParse(release.Tag, out version) ? (version, release) : default)
                .Where(r => r != default)
                .OrderByDescending(r => r.version)
                .Select(r =>
                {
                    var (version, release) = r;
                    var asset = release.Assets.FirstOrDefault(a => a.Name == "Wabbajack.exe");
                    return asset != default ? (version, release, asset) : default;
                })
                .FirstOrDefault();

            var launcherFolder = AbsolutePath.EntryPoint.Parent;
            var exePath = launcherFolder.Combine("Wabbajack.exe");
            
            var launcherVersion = FileVersionInfo.GetVersionInfo(exePath.ToString());

            if (release != default && launcherVersion != null && release.version > Version.Parse(launcherVersion.FileVersion!))
            {
                Utils.Log($"Updating Launcher from {launcherVersion.FileVersion} to {release.version}");
                var tempPath = launcherFolder.Combine("Wabbajack.exe.temp");
                var client = new Client();
                client.UseChromeUserAgent();
                await client.DownloadAsync(release.asset.BrowserDownloadUrl!, tempPath);

                if (tempPath.Size != release.asset.Size)
                {
                    Utils.Log(
                        $"Downloaded launcher did not match expected size: {tempPath.Size} expected {release.asset.Size}");
                    return;
                }

                if (exePath.Exists)
                    await exePath.DeleteAsync();
                await tempPath.MoveToAsync(exePath);
                
                Utils.Log("Finished updating wabbajack");
                await Metrics.Send("updated_launcher", $"{launcherVersion.FileVersion} -> {release.version}");
            }
        }
        
        private static async Task<Release[]> GetReleases()
        {
            Utils.Log("Getting new Wabbajack version list");
            var client = new Client();
            client.UseChromeUserAgent();
            return await client.GetJsonAsync<Release[]>(Consts.GITHUB_REPO_RELEASES.ToString());
        }


        class Release
        {
            [JsonProperty("tag_name")] public string Tag { get; set; } = "";

            [JsonProperty("assets")] public Asset[] Assets { get; set; } = Array.Empty<Asset>();

        }

        class Asset
        {
            [JsonProperty("browser_download_url")]
            public Uri? BrowserDownloadUrl { get; set; }

            [JsonProperty("name")] public string Name { get; set; } = "";

            [JsonProperty("size")] public long Size { get; set; } = 0;
        }
    }
}
