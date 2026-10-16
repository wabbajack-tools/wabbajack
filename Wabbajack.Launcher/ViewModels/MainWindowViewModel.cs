using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reactive.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using MsBox.Avalonia.Dto;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Wabbajack.Common;
using Wabbajack.Downloaders.Http;
using Wabbajack.DTOs.Logins;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.Networking.NexusApi;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
#pragma warning disable SYSLIB0014

namespace Wabbajack.Launcher.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly WebClient _client = new();
    private readonly List<string> _errors = new();

    private (Version Version, long Size, Func<Task<Uri>> Uri) _version;
    public Uri GITHUB_REPO = new("https://api.github.com/repos/wabbajack-tools/wabbajack/releases");
    private readonly NexusApi _nexusApi;
    private readonly HttpDownloader _downloader;
    private readonly ITokenProvider<NexusOAuthState> _tokenProvider;

    public MainWindowViewModel(NexusApi nexusApi, HttpDownloader downloader, ITokenProvider<NexusOAuthState> tokenProvider)
    {
        AppDomain.CurrentDomain.UnhandledException += async (sender, e) =>
        {
            var msg = MsBox.Avalonia.MessageBoxManager
                .GetMessageBoxStandard(new MessageBoxStandardParams()
                {
                    Topmost = true,
                    ShowInCenter = true,
                    ContentTitle = "Wabbajack Launcher: Uncaught error",
                    ContentMessage =
                        $"Uncaught exception! For help, please share this in the Wabbajack Discord. Exception: {((Exception)e.ExceptionObject).ToString()}"
                });
            var result = await msg.ShowAsync();
        };

        _nexusApi = nexusApi;
        Status = "Checking for new versions";
        _downloader = downloader;
        _tokenProvider = tokenProvider;

        this.WhenAnyValue(vm => vm.Status)
            .Buffer(2, 1)
            .Select(b => b[0])
            .Subscribe(s => {
                if (s.StartsWith("Extracting") || s.StartsWith("Downloading")) return;

                if (StatusHistory.Count >= 3)
                    StatusHistory.RemoveAt(0);

                StatusHistory.Add(s);
            });
        var tsk = CheckForUpdates();
    }

    [Reactive] public partial ObservableCollection<string> StatusHistory { get; set; } = new ObservableCollection<string>();
    [Reactive] public partial string Status { get; set; }

    private async Task CheckForUpdates()
    {
        await VerifyCurrentLocation();

        _client.Headers.Add("user-agent", "Wabbajack Launcher");
        Status = "Selecting Release";

        try
        {

            // Nexus carries the Windows build only as far as this launcher is concerned; Linux reads GitHub.
            if (OperatingSystem.IsWindows() && _tokenProvider.HaveToken())
            {
                try
                {
                    _version = await GetNexusReleases(CancellationToken.None);
                }
                catch (Exception)
                {
                    _errors.Add("Nexus error");
                }
            }

            if (_version == default)
            {
                _version = await GetGithubRelease(CancellationToken.None);
            }


        }
        catch (Exception ex)
        {
            _errors.Add(ex.Message);
            await FinishAndExit();
        }

        if (_version == default)
        {
            _errors.Add("Unable to find releases");
            await FinishAndExit();
        }

        Status = "Looking for Updates";

        var baseFolder = InstallRoot.Combine(_version.Version.ToString());

        if (baseFolder.Combine(AppExecutable).FileExists()) await FinishAndExit();

        Status = $"Getting download Uri for {_version.Version}";
        var uri = await _version.Uri();

        /*
        var archive = new Archive()
        {
            Name = $"{_version.Version}.zip",
            Size = _version.Size,
            State = new Http {Url = uri}
        };

        await using var stream = await _downloader.GetChunkedSeekableStream(archive, CancellationToken.None);
        var rdr = new ZipReader(stream, true);
        var entries = (await rdr.GetFiles()).OrderBy(d => d.FileOffset).ToArray();
        foreach (var file in  entries)
        {
            if (file.FileName.EndsWith("/") || file.FileName.EndsWith("\\")) continue;
            var relPath = file.FileName.ToRelativePath();
            Status = $"Extracting: {relPath.FileName}";
            var outPath = baseFolder.Combine(relPath);
            if (!outPath.Parent.DirectoryExists())
                outPath.Parent.CreateDirectory();

            await using var of = outPath.Open(FileMode.Create, FileAccess.Write, FileShare.None);
            await rdr.Extract(file, of, CancellationToken.None);
        }*/


        var wc = new WebClient();
        wc.DownloadProgressChanged += (sender, args) => UpdateProgress($"{_version.Version}", sender, args);
        Status = $"Downloading {_version.Version} ...";
        byte[] data;
        try
        {
            data = await wc.DownloadDataTaskAsync(uri);
        }
        catch (Exception ex)
        {
            _errors.Add(ex.Message);
            // Something went wrong so fallback to original URL
            try
            {
                data = await wc.DownloadDataTaskAsync(uri);
            }
            catch (Exception ex2)
            {
                _errors.Add(ex2.Message);
                await FinishAndExit();
                throw; // avoid unsigned variable 'data'
            }
        }

        if (!OperatingSystem.IsWindows())
        {
            try
            {
                Status = $"Extracting {_version.Version}";
                LinuxReleases.Extract(new MemoryStream(data), baseFolder.ToString());
            }
            catch (Exception ex)
            {
                _errors.Add(ex.Message);
            }

            await FinishAndExit();
        }

        try
        {
            using var zip = new ZipArchive(new MemoryStream(data), ZipArchiveMode.Read);
            foreach (var entry in zip.Entries)
            {
                Status = $"Extracting: {entry.Name}";
                var outPath = baseFolder.Combine(entry.FullName.ToRelativePath());
                if (!outPath.Parent.DirectoryExists())
                    outPath.Parent.CreateDirectory();

                if (entry.FullName.EndsWith("/") || entry.FullName.EndsWith("\\"))
                    continue;
                await using var o = entry.Open();
                await using var of = outPath.Open(FileMode.Create, FileAccess.Write, FileShare.None);
                await o.CopyToAsync(of);
            }
        }
        catch (Exception ex)
        {
            _errors.Add(ex.Message);
        }

        await FinishAndExit();
    }

    private async Task VerifyCurrentLocation()
    {
        try
        {
            var entryPoint = KnownFolders.EntryPoint;
            if (KnownFolders.IsInSpecialFolder(entryPoint, out var _) || entryPoint.Depth <= 1)
            {
                var msg = MsBox.Avalonia.MessageBoxManager
                    .GetMessageBoxStandard(new MessageBoxStandardParams()
                    {
                        Topmost = true,
                        ShowInCenter = true,
                        ContentTitle = "Wabbajack Launcher: Bad startup path",
                        ContentMessage =
                            "Cannot start in the root of a drive, or protected folder locations such as Downloads, Desktop etc.\nPlease move Wabbajack to another folder, creating a new folder if necessary ( example : C:\\Wabbajack\\), outside of these locations."
                    });
                var result = await msg.ShowAsync();
                Environment.Exit(1);
            }
        }
        catch (Exception ex)
        {
            Status = ex.Message;
            var msg = MsBox.Avalonia.MessageBoxManager
                .GetMessageBoxStandard(new MessageBoxStandardParams()
                {
                    Topmost = true,
                    ShowInCenter = true,
                    ContentTitle = "Wabbajack Launcher: Error",
                    ContentMessage = ex.ToString()
                });
            var result = await msg.ShowAsync();
            Environment.Exit(1);
        }
    }

    [ContractAnnotation("=> halt")]
    private async Task FinishAndExit()
    {
        try
        {
            Status = "Launching...";
            var wjFolder = InstallRoot.EnumerateDirectories(recursive:false)
                .OrderByDescending(v =>
                    Version.TryParse(v.FileName.ToString(), out var ver) ? ver : new Version(0, 0, 0, 0))
                .FirstOrDefault();

            if (wjFolder == default)
            {
                _errors.Add("No WJ install found");
                throw new Exception("No WJ install found");
            }

            var filename = wjFolder.Combine(AppExecutable);
            if (OperatingSystem.IsWindows())
            {
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
            else
            {
                await CreateCliScript(filename);
                var info = new ProcessStartInfo(filename.ToString()) {WorkingDirectory = wjFolder.ToString()};
                foreach (var arg in Environment.GetCommandLineArgs().Skip(1)) info.ArgumentList.Add(arg);
                Process.Start(info);
            }
        }
        catch (Exception ex)
        {
            _errors.Add(ex.Message);
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
        try
        {
            filename = filename.Parent.Combine("cli", "wabbajack-cli.exe");
            var data = $"\"{filename}\" %*";
            var file = Path.Combine(Directory.GetCurrentDirectory(), "wabbajack-cli.bat");
            if (File.Exists(file) && await File.ReadAllTextAsync(file) == data) return;
            var parent = Directory.GetParent(file)!.FullName;
            if (!Directory.Exists(file))
                Directory.CreateDirectory(parent);
            await File.WriteAllTextAsync(file, data);
        }
        catch (Exception ex)
        {
            _errors.Add($"Creating Batch File : {ex}");
        }
    }

    private void UpdateProgress(string what, object sender, DownloadProgressChangedEventArgs e)
    {
        Status = $"Downloading {what} ({e.ProgressPercentage}%)...";
    }

    /// <summary>
    ///     Where version folders go. On Windows the working directory, as it always was; a Linux desktop starts
    ///     programs in the home folder, so there it is the launcher's own folder.
    /// </summary>
    private static AbsolutePath InstallRoot =>
        OperatingSystem.IsWindows() ? KnownFolders.CurrentDirectory : KnownFolders.EntryPoint;

    private static string AppExecutable => OperatingSystem.IsWindows() ? "Wabbajack.exe" : "Wabbajack";

    /// <summary>The Linux counterpart of the .bat file: wabbajack-cli beside the launcher, running the newest CLI.</summary>
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    private async Task CreateCliScript(AbsolutePath app)
    {
        try
        {
            var script = Path.Combine(InstallRoot.ToString(), "wabbajack-cli");
            var cli = app.Parent.Combine("cli", "wabbajack-cli").ToString().Replace("'", "'\\''");
            var data = $"#!/bin/sh\nexec '{cli}' \"$@\"\n";
            if (!File.Exists(script) || await File.ReadAllTextAsync(script) != data)
                await File.WriteAllTextAsync(script, data);
            File.SetUnixFileMode(script, File.GetUnixFileMode(script) | UnixFileMode.UserExecute |
                                         UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
        }
        catch (Exception ex)
        {
            _errors.Add($"Creating CLI script : {ex}");
        }
    }

    private async Task<(Version Version, long Size, Func<Task<Uri>> Uri)> GetGithubRelease(CancellationToken token)
    {
        var releases = await GetGithubReleases();

        if (!OperatingSystem.IsWindows())
        {
            var linux = LinuxReleases.Pick(releases.Select(r => new ReleaseInfo(r.Tag, r.Prerelease,
                r.Assets.Select(a => new ReleaseFile(a.Name, a.BrowserDownloadUrl, a.Size)).ToList())));
            if (linux == null)
            {
                Status = "No release has a Linux build yet";
                return default;
            }

            var (linuxVersion, file) = linux.Value;
            return (linuxVersion, file.Size, async () => file.Url);
        }


        var version = releases.Select(r =>
        {
            if (r.Tag.Split(".").Length == 4 && Version.TryParse(r.Tag, out var v))
                return (v, r);
            return (new Version(0, 0, 0, 0), r);
        })
            .OrderByDescending(r => r.Item1)
            .FirstOrDefault();

        var asset = version.r.Assets.FirstOrDefault(a => a.Name == version.Item1 + ".zip");
        if (asset == null)
        {
            Status = $"Error, no asset found for Github Release {version.r}";
            return default;
        }

        return (version.Item1, asset.Size, async () => asset!.BrowserDownloadUrl);
    }

    private async Task<Release[]> GetGithubReleases()
    {
        Status = "Checking GitHub Repository";
        var data = await _client.DownloadStringTaskAsync(GITHUB_REPO);
        Status = "Parsing Response";
        return JsonSerializer.Deserialize<Release[]>(data)!;
    }

    private async Task<(Version Version, long Size, Func<Task<Uri>> uri)> GetNexusReleases(CancellationToken token)
    {
        Status = "Checking Nexus for updates";
        if (!await _nexusApi.IsPremium(token))
            return default;

        var data = await _nexusApi.ModFiles("site", 403, token);
        Status = "Parsing Response";
        //return JsonSerializer.Deserialize<Release[]>(data)!;

        var found = data.info.Files.Where(f => f.CategoryId == 5 || f.CategoryId == 3)
            .Where(f => f.Name.EndsWith(".zip"))
            .Select(f => Version.TryParse(f.Name[..^4], out var version) ? (version, f.SizeInBytes ?? f.Size,  f.FileId) : default)
            .FirstOrDefault(f => f != default);
        if (found == default) return default;

        return (found.version, found.Item2, async () =>
        {
            var link = await _nexusApi.DownloadLink("site", 403, found.FileId, token);

            return link.info.First().URI;
        });
    }


    private partial class Release
    {
        [JsonPropertyName("tag_name")] public string Tag { get; set; }

        [JsonPropertyName("prerelease")] public bool Prerelease { get; set; }

        [JsonPropertyName("assets")] public Asset[] Assets { get; set; }
    }

    private partial class Asset
    {
        [JsonPropertyName("browser_download_url")]
        public Uri BrowserDownloadUrl { get; set; }

        [JsonPropertyName("name")] public string Name { get; set; }


        [JsonPropertyName("size")] public long Size { get; set; }
    }
}
