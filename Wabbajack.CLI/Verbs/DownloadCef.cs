using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.BZip2;
using ICSharpCode.SharpZipLib.Tar;
using Microsoft.Extensions.Logging;
using Wabbajack.CLI.DTOs;
using Wabbajack.Downloaders;
using Wabbajack.DTOs;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Version = System.Version;

namespace Wabbajack.CLI.Verbs;

public class DownloadCef : IVerb
{
    private readonly DownloadDispatcher _dispatcher;
    private readonly FileExtractor.FileExtractor _fileExtractor;
    private readonly HttpClient _httpClient;
    private readonly ILogger<DownloadCef> _logger;

    public DownloadCef(ILogger<DownloadCef> logger, DownloadDispatcher dispatcher,
        FileExtractor.FileExtractor fileExtractor, HttpClient httpClient)
    {
        _logger = logger;
        _dispatcher = dispatcher;
        _fileExtractor = fileExtractor;
        _httpClient = httpClient;
    }

    public Command MakeCommand()
    {
        var command = new Command("download-cef");
        command.Add(new Option<AbsolutePath>(new[] {"-f", "-folder"}, "Path to Wabbajack"));
        command.Add(new Option<bool>(new[] {"--force"}, "Force the download even if the output already exists"));
        command.Description = "Downloads CEF into this folder";
        command.Handler = CommandHandler.Create(Run);
        return command;
    }


    public async Task<int> Run(AbsolutePath folder, bool force = false)
    {
        if (folder == default) folder = KnownFolders.EntryPoint;

        var cefNet = folder.Combine("CefNet.dll");
        if (!cefNet.FileExists())
        {
            _logger.LogError("Cannot find CefNet.dll in {folder}", folder);
            return 1;
        }

        var version = Version.Parse(FileVersionInfo.GetVersionInfo(cefNet.ToString()).FileVersion!);
        var downloadVersion = $"{version.Major}.{version.Minor}";
        var runtime = RuntimeInformation.RuntimeIdentifier;
        if (folder.Combine("libcef.dll").FileExists() && !force)
        {
            _logger.LogInformation("Not downloading, cef already exists");
            return 0;
        }

        _logger.LogInformation("Downloading Cef version {version} for {runtime}", downloadVersion, runtime);

        var versions = await CefCDNResponse.Load(_httpClient);

        var findSource = versions.FindSource(downloadVersion);

        var fileUri = new Uri($"https://cef-builds.spotifycdn.com/{findSource.Name}");

        var parsed = _dispatcher.Parse(fileUri);
        var tempFile = folder.Combine(findSource.Name);
        await _dispatcher.Download(new Archive {State = parsed!}, tempFile, CancellationToken.None);

        {
            _logger.LogInformation("Extracting {file}", tempFile);

            await using var istream = tempFile.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
            await using var bzip2 = new BZip2InputStream(istream);
            await using var tar = new TarInputStream(bzip2, Encoding.UTF8);
            var prefix = tempFile.FileName.WithoutExtension().WithoutExtension().Combine("Release");
            var fullPrefix = prefix.RelativeTo(folder);
            while (true)
            {
                var entry = tar.GetNextEntry();
                if (entry == null) break;

                var path = entry.Name.ToRelativePath();

                if (path.InFolder(prefix) && entry.Size > 0)
                {
                    var outputPath = path.RelativeTo(folder).RelativeTo(fullPrefix).RelativeTo(folder);
                    outputPath.Parent.CreateDirectory();

                    _logger.LogInformation("Extracting {FileName} to {Folder}", outputPath.FileName,
                        outputPath.RelativeTo(folder));
                    await using var os = outputPath.Open(FileMode.Create, FileAccess.Write);
                    tar.CopyEntryContents(os);
                }
            }
        }

        tempFile.Delete();

        return 0;
    }
}