using Microsoft.Extensions.Logging;
using Wabbajack.DTOs;
using Wabbajack.Networking.NexusApi;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.Translation.Fomod;
using Wabbajack.Translation.Nexus;

namespace Wabbajack.Translation;

public sealed record LanguagePackResult(bool Installed, int FilesWritten, string? Problem);

public sealed class LanguagePackInstaller
{
    private readonly NexusApi _nexus;
    private readonly TranslationDownloader _downloader;
    private readonly FileExtractor.FileExtractor _extractor;
    private readonly TemporaryFileManager _temp;
    private readonly ILogger<LanguagePackInstaller> _logger;

    public LanguagePackInstaller(NexusApi nexus, TranslationDownloader downloader,
        FileExtractor.FileExtractor extractor, TemporaryFileManager temp, ILogger<LanguagePackInstaller> logger)
    {
        _nexus = nexus;
        _downloader = downloader;
        _extractor = extractor;
        _temp = temp;
        _logger = logger;
    }

    public async Task<LanguagePackResult> Install(Game game, LanguagePack pack, AbsolutePath downloads,
        AbsolutePath output, IManualTranslationDownloads? manual, CancellationToken token)
    {
        var domain = game.MetaData().NexusDomain;
        Networking.NexusApi.DTOs.ModFile? file;
        try
        {
            var (files, _) = await _nexus.ModFiles(domain, pack.ModId, token);
            file = files.Files
                .Where(f => string.Equals(f.CategoryName, "MAIN", StringComparison.OrdinalIgnoreCase) &&
                            (pack.FileNameContains == null ||
                             f.Name.Contains(pack.FileNameContains, StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(f => f.UploadedTimestamp)
                .FirstOrDefault();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning("Could not list the files of {Pack}: {Message}", pack.Name, ex.Message);
            return new LanguagePackResult(false, 0, $"{pack.Name} could not be looked up on Nexus Mods");
        }

        if (file == null)
            return new LanguagePackResult(false, 0, $"{pack.Name} has no current main file on Nexus Mods");

        var translationFile = new TranslationFile(game, pack.ModId, file.FileId, pack.Name, file.FileName, file.Name,
            file.CategoryName ?? "", file.SizeInBytes ?? file.SizeKb * 1024L,
            DateTimeOffset.FromUnixTimeSeconds(file.UploadedTimestamp).UtcDateTime, []);
        var downloaded = await _downloader.Download([translationFile], downloads, manual, null, token);
        if (!downloaded.TryGetValue(translationFile, out var archive))
            return new LanguagePackResult(false, 0, $"{pack.Name} was not downloaded");

        await using var work = _temp.CreateFolder();
        await _extractor.ExtractAll(archive, work.Path, token, _ => true);
        var written = await SimpleFomodInstaller.Install(work.Path, output, token);
        _logger.LogInformation("Installed {Count} files from {Pack} ({File})", written, pack.Name, file.Name);
        return new LanguagePackResult(written > 0, written, written > 0 ? null : $"{pack.Name} installed no files");
    }
}
