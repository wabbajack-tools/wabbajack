using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Wabbajack.Compression.BSA;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Translation;

public sealed record LanguageSupportResult(
    int FilesWritten,
    int AlreadyTranslated,
    IReadOnlyList<string> EnglishVoiceArchives);

public sealed class LanguageSupportBuilder
{
    private readonly ILogger<LanguageSupportBuilder> _logger;

    public LanguageSupportBuilder(ILogger<LanguageSupportBuilder> logger)
    {
        _logger = logger;
    }

    private static Regex EnglishText(string englishSuffix) => new(
        @"^((?:interface[\\/]translations[\\/].*)|(?:strings[\\/].*))_" + Regex.Escape(englishSuffix) +
        @"(\.txt|\.strings|\.dlstrings|\.ilstrings)$",
        RegexOptions.IgnoreCase);

    public static string? LocalizedName(string relativePath, string englishSuffix, string targetSuffix)
    {
        var match = EnglishText(englishSuffix).Match(relativePath);
        return match.Success ? match.Groups[1].Value + "_" + targetSuffix + match.Groups[2].Value : null;
    }

    public async Task<LanguageSupportResult> Build(Mo2Instance instance, GameLanguage language,
        AbsolutePath outputMod, Func<string, bool> isOwnMod, CancellationToken token)
    {
        var englishSuffix = instance.Support.EnglishStringsSuffix;
        var voices = EnglishVoiceArchives(instance);
        if (language.StringsSuffix.Equals(englishSuffix, StringComparison.OrdinalIgnoreCase))
            return new LanguageSupportResult(0, 0, voices);

        var englishText = EnglishText(englishSuffix);
        var sources = new Dictionary<string, (AbsolutePath Archive, string Entry, AbsolutePath? Loose)>(
            StringComparer.OrdinalIgnoreCase);
        var localized = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var suffix = "_" + language.StringsSuffix + ".";

        foreach (var mod in instance.EnabledModsByPriority.Where(m => !isOwnMod(m)))
        {
            token.ThrowIfCancellationRequested();
            var folder = instance.ModsFolder.Combine(mod);
            if (!folder.DirectoryExists()) continue;

            foreach (var file in folder.EnumerateFiles(recursive: true))
                Note(file.RelativeTo(folder).ToString(), (default, "", file));

            foreach (var archive in folder.EnumerateFiles(recursive: false)
                         .Where(f => f.Extension.ToString().ToLowerInvariant() is ".ba2" or ".bsa"))
            {
                try
                {
                    var reader = await BSADispatch.Open(archive);
                    foreach (var entry in reader.Files)
                        Note(entry.Path.ToString(), (archive, entry.Path.ToString(), null));
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning("Could not read {Archive}: {Message}", archive, ex.Message);
                }
            }
        }

        var written = 0;
        var skipped = 0;
        foreach (var (relative, source) in sources)
        {
            token.ThrowIfCancellationRequested();
            var target = LocalizedName(relative, englishSuffix, language.StringsSuffix)!;
            var output = outputMod.Combine(target.Replace('/', '\\'));
            if (localized.Contains(target) || output.FileExists())
            {
                skipped++;
                continue;
            }

            output.Parent.CreateDirectory();
            if (source.Loose is { } loose)
            {
                await output.WriteAllBytesAsync(await loose.ReadAllBytesAsync(token), token);
            }
            else
            {
                var reader = await BSADispatch.Open(source.Archive);
                var entry = reader.Files.First(f =>
                    f.Path.ToString().Equals(source.Entry, StringComparison.OrdinalIgnoreCase));
                await using var stream = output.Open(FileMode.Create, FileAccess.Write, FileShare.None);
                await entry.CopyDataTo(stream, token);
            }

            written++;
        }

        return new LanguageSupportResult(written, skipped, voices);

        void Note(string relative, (AbsolutePath, string, AbsolutePath?) source)
        {
            var normalized = relative.Replace('/', '\\');
            if (normalized.Contains(suffix, StringComparison.OrdinalIgnoreCase) &&
                (normalized.StartsWith("interface\\translations\\", StringComparison.OrdinalIgnoreCase) ||
                 normalized.StartsWith("strings\\", StringComparison.OrdinalIgnoreCase)))
                localized.Add(normalized);
            if (englishText.IsMatch(normalized))
                sources.TryAdd(normalized, source);
        }
    }

    private static IReadOnlyList<string> EnglishVoiceArchives(Mo2Instance instance)
    {
        if (instance.Support.EnglishPluginVoicesPattern is not { } pattern) return [];
        var regex = new Regex(pattern, RegexOptions.IgnoreCase);
        return instance.DataRootsByPriority()
            .Where(r => r.DirectoryExists())
            .SelectMany(r => r.EnumerateFiles(recursive: false))
            .Select(f => f.FileName.ToString())
            .Where(n => regex.IsMatch(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
