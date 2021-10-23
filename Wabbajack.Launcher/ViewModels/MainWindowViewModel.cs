using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ReactiveUI.Fody.Helpers;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Launcher.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly WebClient _client = new();
    private readonly List<string> _errors = new();

    private Release _version;
    public Uri GITHUB_REPO = new("https://api.github.com/repos/wabbajack-tools/wabbajack/releases");

    public MainWindowViewModel()
    {
        Status = "Checking for new versions";
        var tsk = CheckForUpdates();
    }

    [Reactive] public string Status { get; set; }

    private async Task CheckForUpdates()
    {
        _client.Headers.Add("user-agent", "Wabbajack Launcher");
        Status = "Selecting Release";

        try
        {
            var releases = await GetReleases();
            _version = releases.OrderByDescending(r =>
            {
                if (r.Tag.Split(".").Length == 4 && Version.TryParse(r.Tag, out var v))
                    return v;
                return new Version(0, 0, 0, 0);
            }).FirstOrDefault();
        }
        catch (Exception ex)
        {
            _errors.Add(ex.Message);
            await FinishAndExit();
        }

        if (_version == null)
        {
            _errors.Add("Unable to parse Github releases");
            await FinishAndExit();
        }

        Status = "Looking for Updates";

        var base_folder = Path.Combine(Directory.GetCurrentDirectory(), _version.Tag);

        if (File.Exists(Path.Combine(base_folder, "Wabbajack.exe"))) await FinishAndExit();

        var asset = _version.Assets.FirstOrDefault(a => a.Name == _version.Tag + ".zip");
        if (asset == null)
        {
            _errors.Add("No zip file for release " + _version.Tag);
            await FinishAndExit();
        }

        var wc = new WebClient();
        wc.DownloadProgressChanged += UpdateProgress;
        Status = $"Downloading {_version.Tag} ...";
        byte[] data;
        try
        {
            data = await wc.DownloadDataTaskAsync(asset.BrowserDownloadUrl);
        }
        catch (Exception ex)
        {
            _errors.Add(ex.Message);
            // Something went wrong so fallback to original URL
            try
            {
                data = await wc.DownloadDataTaskAsync(asset.BrowserDownloadUrl);
            }
            catch (Exception ex2)
            {
                _errors.Add(ex2.Message);
                await FinishAndExit();
                throw; // avoid unsigned variable 'data'
            }
        }

        try
        {
            using (var zip = new ZipArchive(new MemoryStream(data), ZipArchiveMode.Read))
            {
                foreach (var entry in zip.Entries)
                {
                    Status = $"Extracting: {entry.Name}";
                    var outPath = Path.Combine(base_folder, entry.FullName);
                    if (!Directory.Exists(Path.GetDirectoryName(outPath)))
                        Directory.CreateDirectory(Path.GetDirectoryName(outPath));

                    if (entry.FullName.EndsWith("/") || entry.FullName.EndsWith("\\"))
                        continue;
                    await using var o = entry.Open();
                    await using var of = File.Create(outPath);
                    await o.CopyToAsync(of);
                }
            }
        }
        catch (Exception ex)
        {
            _errors.Add(ex.Message);
        }
        finally
        {
            await FinishAndExit();
        }
    }

    private async Task FinishAndExit()
    {
        try
        {
            Status = "Launching...";
            var wjFolder = KnownFolders.CurrentDirectory.EnumerateDirectories()
                .OrderByDescending(v =>
                    Version.TryParse(v.FileName.ToString(), out var ver) ? ver : new Version(0, 0, 0, 0))
                .FirstOrDefault();

            var filename = wjFolder.Combine("Wabbajack.exe");
            await CreateBatchFile(filename);
            var info = new ProcessStartInfo
            {
                FileName = filename.ToString(),
                Arguments = string.Join(" ",
                    Environment.GetCommandLineArgs().Skip(1).Select(s => s.Contains(' ') ? '\"' + s + '\"' : s)),
                WorkingDirectory = wjFolder.ToString()
            };
            Process.Start(info);
        }
        catch (Exception)
        {
            if (_errors.Count == 0)
            {
                Status = "Failed: Unknown error";
                await Task.Delay(10000);
            }

            foreach (var error in _errors)
            {
                Status = "Failed: " + error;
                await Task.Delay(10000);
            }
        }
        finally
        {
            Environment.Exit(0);
        }
    }

    private async Task CreateBatchFile(AbsolutePath filename)
    {
        filename = filename.Parent.Combine("wabbajack-cli.exe");
        var data = $"\"{filename}\" %*";
        var file = Path.Combine(Directory.GetCurrentDirectory(), "wabbajack-cli.bat");
        if (File.Exists(file) && await File.ReadAllTextAsync(file) == data) return;
        await File.WriteAllTextAsync(file, data);
    }

    private void UpdateProgress(object sender, DownloadProgressChangedEventArgs e)
    {
        Status = $"Downloading {_version.Tag} ({e.ProgressPercentage}%)...";
    }

    private async Task<Release[]> GetReleases()
    {
        Status = "Checking GitHub Repository";
        var data = await _client.DownloadStringTaskAsync(GITHUB_REPO);
        Status = "Parsing Response";
        return JsonSerializer.Deserialize<Release[]>(data)!;
    }


    private class Release
    {
        [JsonPropertyName("tag_name")] public string Tag { get; set; }

        [JsonPropertyName("assets")] public Asset[] Assets { get; set; }
    }

    private class Asset
    {
        [JsonPropertyName("browser_download_url")]
        public Uri BrowserDownloadUrl { get; set; }

        [JsonPropertyName("name")] public string Name { get; set; }
    }
}