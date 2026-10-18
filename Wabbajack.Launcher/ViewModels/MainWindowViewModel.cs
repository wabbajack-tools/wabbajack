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
using Wabbajack.Launcher.Platforms;
using Wabbajack.Paths.IO;
#pragma warning disable SYSLIB0014

namespace Wabbajack.Launcher.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly WebClient _client = new();
    private readonly List<string> _errors = new();

    private (Version Version, long Size, Func<Task<Uri>> Uri) _version;
    // WABBAJACK_LAUNCHER_RELEASES points the launcher at another release list, to test a launcher against
    // releases that are not published; unset, it reads Wabbajack's own.
    public Uri GITHUB_REPO = new(Environment.GetEnvironmentVariable("WABBAJACK_LAUNCHER_RELEASES") is {Length: > 0} url
        ? url
        : "https://api.github.com/repos/wabbajack-tools/wabbajack/releases");
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

            if (Platform.ChecksNexus && _tokenProvider.HaveToken())
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

        var baseFolder = Platform.InstallRoot.Combine(_version.Version.ToString());

        if (baseFolder.Combine(Platform.AppExecutable).FileExists()) await FinishAndExit();

        Status = $"Getting download Uri for {_version.Version}";
        var uri = await _version.Uri();



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

        try
        {
            Status = $"Extracting {_version.Version}";
            Platform.Unpack(data, baseFolder);
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
            var wjFolder = Platform.InstallRoot.EnumerateDirectories(recursive:false)
                .OrderByDescending(v =>
                    Version.TryParse(v.FileName.ToString(), out var ver) ? ver : new Version(0, 0, 0, 0))
                .FirstOrDefault();

            if (wjFolder == default)
            {
                _errors.Add("No WJ install found");
                throw new Exception("No WJ install found");
            }

            var app = wjFolder.Combine(Platform.AppExecutable);
            try
            {
                await Platform.WriteCliShortcut(app);
            }
            catch (Exception ex)
            {
                _errors.Add($"Creating the CLI shortcut : {ex}");
            }

            Process.Start(Platform.StartApp(app, Environment.GetCommandLineArgs().Skip(1).ToArray()));
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

    private void UpdateProgress(string what, object sender, DownloadProgressChangedEventArgs e)
    {
        Status = $"Downloading {what} ({e.ProgressPercentage}%)...";
    }

    private static ILauncherPlatform Platform => LauncherPlatform.Current;

    private async Task<(Version Version, long Size, Func<Task<Uri>> Uri)> GetGithubRelease(CancellationToken token)
    {
        var releases = await GetGithubReleases();
        var picked = Platform.PickGitHubRelease(releases.Select(r => new ReleaseInfo(r.Tag, r.Prerelease,
            r.Assets.Select(a => new ReleaseFile(a.Name, a.BrowserDownloadUrl, a.Size)).ToList())).ToList(), out var whyNot);
        if (picked == null)
        {
            Status = whyNot;
            return default;
        }

        var (version, file) = picked.Value;
        return (version, file.Size, async () => file.Url);
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
