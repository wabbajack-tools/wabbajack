using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Wabbajack.Common;
using Wabbajack.Downloaders;
using Wabbajack.DTOs;
using Wabbajack.Networking.NexusApi;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using Wabbajack.Translation.Plugins;

namespace Wabbajack.Translation.Nexus;

public interface IManualTranslationDownloads
{
    Task<IReadOnlyDictionary<TranslationFile, AbsolutePath>> Acquire(IReadOnlyList<TranslationFile> files,
        AbsolutePath destination, CancellationToken token);
}

public sealed class TranslationDownloader
{
    private readonly DownloadDispatcher _dispatcher;
    private readonly NexusApi _nexus;
    private readonly FileExtractor.FileExtractor _extractor;
    private readonly IResource<FileExtractor.FileExtractor> _extractLimiter;
    private readonly ILogger<TranslationDownloader> _logger;

    public TranslationDownloader(DownloadDispatcher dispatcher, NexusApi nexus,
        FileExtractor.FileExtractor extractor, IResource<FileExtractor.FileExtractor> extractLimiter,
        ILogger<TranslationDownloader> logger)
    {
        _extractLimiter = extractLimiter;
        _dispatcher = dispatcher;
        _nexus = nexus;
        _extractor = extractor;
        _logger = logger;
    }

    public async Task<IReadOnlyDictionary<TranslationFile, AbsolutePath>> Download(
        IReadOnlyList<TranslationFile> files, AbsolutePath destination, IManualTranslationDownloads? manual,
        Action<string, int, int>? progress, CancellationToken token)
    {
        destination.CreateDirectory();
        var result = new Dictionary<TranslationFile, AbsolutePath>();
        var missing = new List<TranslationFile>();
        foreach (var file in files)
        {
            var path = destination.Combine(file.FileName);
            if (path.FileExists() && path.Size() == file.Size) result[file] = path;
            else missing.Add(file);
        }

        if (missing.Count == 0) return result;

        bool premium;
        try
        {
            premium = await _nexus.IsPremium(token);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogInformation("Nexus premium status unknown ({Message}), treating as not premium", ex.Message);
            premium = false;
        }

        if (premium)
        {
            var done = 0;
            foreach (var file in missing)
            {
                token.ThrowIfCancellationRequested();
                progress?.Invoke(file.DisplayName, done++, missing.Count);
                var path = destination.Combine(file.FileName);
                try
                {
                    var archive = new Archive
                    {
                        Name = file.FileName,
                        Size = file.Size,
                        State = new DTOs.DownloadStates.Nexus
                        {
                            Game = file.Game, ModID = file.ModId, FileID = file.FileId, Name = file.ModName
                        }
                    };
                    await _dispatcher.Download(archive, path, token, false);
                    if (path.FileExists()) result[file] = path;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning("Could not download {File} from Nexus mod {ModId}: {Message}", file.FileName,
                        file.ModId, ex.Message);
                }
            }

            return result;
        }

        if (manual == null)
        {
            _logger.LogInformation("{Count} translation files need a Nexus premium account or a manual download",
                missing.Count);
            return result;
        }

        foreach (var (file, path) in await manual.Acquire(missing, destination, token))
            result[file] = path;
        return result;
    }

    private static bool IsStringsFor(RelativePath path, IReadOnlyList<string> stems)
    {
        var name = path.FileName.ToString();
        var extension = path.Extension.ToString().ToLowerInvariant();
        return extension is ".strings" or ".dlstrings" or ".ilstrings" &&
               path.Parent.FileName.ToString().Equals("strings", StringComparison.OrdinalIgnoreCase) &&
               stems.Any(s => name.StartsWith(s, StringComparison.OrdinalIgnoreCase));
    }

    private static readonly string[] ExtractPatterns = ["*.esp", "*.esm", "*.esl", "*.strings", "*.dlstrings", "*.ilstrings"];
    // Folders named after a plugin like sound\voice\x.esm, match the include patterns
    private static readonly string[] SkippedPatterns = ["*.fuz", "*.xwm", "*.wav", "*.lip", "*.nif", "*.dds", "*.tri"];

    private async Task<bool> ExtractMatching(AbsolutePath archive, AbsolutePath folder, CancellationToken token)
    {
        var relative = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"Extractors\windows-x64\7z.exe"
            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? @"Extractors\linux-x64\7zz"
            : @"Extractors\mac\7zz";
        var sevenZip = relative.ToRelativePath().RelativeTo(KnownFolders.EntryPoint);
        if (!sevenZip.FileExists()) return false;

        token.ThrowIfCancellationRequested();
        folder.CreateDirectory();
        var process = new ProcessHelper
        {
            Path = sevenZip,
            Arguments = new object[] {"x", "-y", "-bso0", "-bsp1", "-ssc-", $"-o\"{folder}\"", archive, "-r"}
                .Concat(ExtractPatterns.Select(p => (object) $"-i!{p}"))
                .Concat(SkippedPatterns.Select(p => (object) $"-xr!{p}"))
                .ToArray()
        };

        using var job = await _extractLimiter.Begin($"Extracting translations from {archive.FileName}", 0, token);
        var totalSize = archive.Size();
        job.Size = totalSize;
        var reported = 0L;
        using var progress = process.Output.Subscribe(line =>
        {
            // 7-Zip redraws its percentage with backspaces
            if (line.Type != ProcessHelper.StreamType.Output) return;
            foreach (var part in line.Line.Split('\b', '\r'))
            {
                var percentAt = part.IndexOf('%');
                if (percentAt <= 0 || !int.TryParse(part[..percentAt].Trim(), out var percent)) continue;
                var position = totalSize / 100 * Math.Clamp(percent, 0, 100);
                if (position <= reported) continue;
                job.ReportNoWait((int) Math.Min(position - reported, int.MaxValue));
                reported = position;
            }
        });

        var exitCode = await process.Start();
        if (exitCode == 0)
        {
            if (totalSize > reported) job.ReportNoWait((int) Math.Min(totalSize - reported, int.MaxValue));
            return true;
        }

        _logger.LogInformation("7-Zip could not pick plugins out of {Archive} (exit code {Code}), extracting all of it",
            archive.FileName, exitCode);
        if (folder.DirectoryExists()) folder.DeleteDirectory();
        return false;
    }

    public async Task<IReadOnlyList<TranslationCandidate>> ExtractPlugins(
        IReadOnlyDictionary<TranslationFile, AbsolutePath> files, AbsolutePath workFolder,
        Action<string, int, int>? progress, CancellationToken token)
    {
        var candidates = new List<TranslationCandidate>();
        var done = 0;
        foreach (var (file, archive) in files)
        {
            token.ThrowIfCancellationRequested();
            progress?.Invoke(file.DisplayName, done++, files.Count);
            var folder = workFolder.Combine($"{file.ModId}_{file.FileId}");
            var wanted = file.Plugins.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var stems = file.Plugins.Select(p => Path.GetFileNameWithoutExtension(p) + "_").ToList();
            try
            {
                if (!await ExtractMatching(archive, folder, token))
                    await _extractor.ExtractAll(archive, folder, token,
                        p => wanted.Contains(p.FileName.ToString()) || IsStringsFor(p, stems));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning("Could not extract {File}: {Message}", archive, ex.Message);
                continue;
            }

            if (!folder.DirectoryExists()) continue;
            foreach (var plugin in folder.EnumerateFiles(recursive: true))
                if (wanted.Contains(plugin.FileName.ToString()))
                    candidates.Add(new TranslationCandidate(plugin.FileName.ToString(), plugin,
                        $"{file.ModName} ({file.ModId}, {file.DisplayName})"));
        }

        return candidates;
    }
}
