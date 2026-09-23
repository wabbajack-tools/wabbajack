using Microsoft.Extensions.Logging;
using Wabbajack.DTOs;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using Wabbajack.Translation.Plugins;
using Wabbajack.Translation.Nexus;
using Wabbajack.Translation.Steam;

namespace Wabbajack.Translation;

public sealed record TranslationRequest(
    AbsolutePath InstallFolder,
    AbsolutePath DownloadsFolder,
    Game Game,
    AbsolutePath GameFolder,
    GameLanguage Language,
    bool DownloadVoices,
    string? Profile = null);

public sealed record TranslationProgress(string Stage, string Text, double Fraction);

public sealed record TranslationSummary(
    int TranslationFilesFound,
    int TranslationFilesDownloaded,
    int PluginsTranslated,
    int StringsTranslated,
    int LanguageFilesWritten,
    IReadOnlyList<string> VoiceArchives,
    IReadOnlyList<string> VoiceArchivesFailed,
    IReadOnlyList<PluginTranslationReport> Reports,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<ProfileTranslation> Profiles);

public sealed record ProfileTranslation(
    string Profile,
    string Mod,
    int PluginsTranslated,
    int StringsTranslated,
    int PatchFiles);

public static class TranslationStages
{
    public const string Analysing = "Analysing load order";
    public const string Searching = "Searching Nexus Mods for translations";
    public const string Downloading = "Downloading translation mods";
    public const string Merging = "Merging translations";
    public const string Language = "Switching game language";
    public const string Voices = "Downloading voice files";
}

public sealed class TranslationRunner
{
    private readonly NexusTranslationFinder _finder;
    private readonly TranslationDownloader _downloader;
    private readonly LanguageSupportBuilder _languageSupport;
    private readonly VoiceDownloader _voices;
    private readonly LanguagePackInstaller _packs;
    private readonly TemporaryFileManager _temp;
    private readonly IResource<FileExtractor.FileExtractor> _workLimiter;
    private readonly ILogger<TranslationRunner> _logger;

    public TranslationRunner(NexusTranslationFinder finder, TranslationDownloader downloader,
        LanguageSupportBuilder languageSupport, VoiceDownloader voices, LanguagePackInstaller packs,
        TemporaryFileManager temp, IResource<FileExtractor.FileExtractor> workLimiter, ILogger<TranslationRunner> logger)
    {
        _workLimiter = workLimiter;
        _packs = packs;
        _finder = finder;
        _downloader = downloader;
        _languageSupport = languageSupport;
        _voices = voices;
        _temp = temp;
        _logger = logger;
    }

    public static string TranslationMod(GameLanguage language, string? profile = null) =>
        profile == null
            ? $"Wabbajack Translation ({language.DisplayName})"
            : $"Wabbajack Translation ({language.DisplayName}, {profile})";

    public static string LanguageSupportMod(GameLanguage language) => $"Wabbajack Language Support ({language.DisplayName})";
    public static string VoicesMod(GameLanguage language) => $"Wabbajack Voices ({language.DisplayName})";
    public static string PatchPlugin(GameLanguage language) => $"WabbajackTranslation_{language.Id}.esp";

    public static bool IsOwnMod(string mod) =>
        mod.StartsWith("Wabbajack Translation (", StringComparison.OrdinalIgnoreCase) ||
        mod.StartsWith("Wabbajack Language Support (", StringComparison.OrdinalIgnoreCase) ||
        mod.StartsWith("Wabbajack Voices (", StringComparison.OrdinalIgnoreCase);

    public async Task<TranslationSummary> Run(TranslationRequest request, IManualTranslationDownloads? manual,
        IProgress<TranslationProgress>? progress, CancellationToken token)
    {
        var language = request.Language;
        var lastStage = "";
        void Report(string stage, string text, double fraction)
        {
            if (stage != lastStage)
            {
                lastStage = stage;
                _logger.LogInformation("Translation stage: {Stage}, {Text}", stage, text);
            }

            progress?.Report(new TranslationProgress(stage, text, Math.Clamp(fraction, 0, 1)));
        }

        Report(TranslationStages.Analysing, "Reading the modlist profiles", 0);
        var profileNames = request.Profile != null ? [request.Profile] : Mo2Instance.ProfileNames(request.InstallFolder);
        if (profileNames.Count == 0)
            throw new InvalidOperationException($"No MO2 profiles found under {request.InstallFolder}");

        var instances = new List<Mo2Instance>();
        foreach (var name in profileNames)
            instances.Add(await Mo2Instance.Load(request.InstallFolder, request.Game, request.GameFolder, name));
        await RemovePreviousRun(instances);
        for (var i = 0; i < instances.Count; i++)
            instances[i] = await Mo2Instance.Load(request.InstallFolder, request.Game, request.GameFolder,
                instances[i].Profile);
        var first = instances[0];
        var perProfileNames = instances.Count > 1;

        var warnings = new List<string>();
        var supportMod = first.ModsFolder.Combine(LanguageSupportMod(language));
        var packFiles = 0;
        foreach (var pack in language.BasePacks)
        {
            Report(TranslationStages.Language, $"Installing {pack.Name}", 0);
            var packResult = await _packs.Install(request.Game, pack, request.DownloadsFolder.Combine("Translations"),
                supportMod, manual, token);
            if (!packResult.Installed)
            {
                warnings.Add($"{packResult.Problem}, so the game was left in its current language");
                if (supportMod.DirectoryExists()) supportMod.DeleteDirectory();
                return new TranslationSummary(0, 0, 0, 0, 0, [], [], [], warnings, []);
            }

            packFiles += packResult.FilesWritten;
            if (request.Game == Game.Fallout4 &&
                instances.All(instance => instance.Locate(@"F4SE\Plugins\Buffout4.dll") == null) &&
                !first.GameFolder.Combine(@"Data\F4SE\Plugins\Buffout4.dll").FileExists())
                warnings.Add($"{pack.Name} recommends Buffout 4, which this modlist does not include");
        }

        var analyses = new Dictionary<string, LoadOrderAnalysis>(StringComparer.OrdinalIgnoreCase);
        var wanted = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < instances.Count; i++)
        {
            var instance = instances[i];
            Report(TranslationStages.Analysing, $"Reading {instance.ActivePlugins.Count} plugins in {instance.Profile}",
                (double) i / instances.Count);
            var analysis = await Task.Run(() => LoadOrderAnalysis.Build(instance, _logger, token), token);
            foreach (var (plugin, chars) in analysis.OwnedChars)
                if (chars > 0 && !instance.BasePlugins.Contains(plugin))
                    wanted[plugin] = Math.Max(wanted.GetValueOrDefault(plugin), chars);
            if (instances.Count == 1) analyses[instance.Profile] = analysis;
        }

        var plugins = wanted.OrderByDescending(kv => kv.Value).Select(kv => kv.Key).ToList();
        Report(TranslationStages.Searching, $"Looking for {language.DisplayName} translations of {plugins.Count} plugins", 0);
        var files = await _finder.Find(request.Game, plugins, language,
            (text, fraction) => Report(TranslationStages.Searching, text, fraction),
            token);
        _logger.LogInformation("Found {Count} {Language} translation files to download", files.Count, language.DisplayName);

        Report(TranslationStages.Downloading, $"{files.Count} translation files to fetch", 0);
        var downloaded = await _downloader.Download(files, request.DownloadsFolder.Combine("Translations"), manual,
            (name, done, total) => Report(TranslationStages.Downloading, $"Downloading {name} ({done + 1}/{total})",
                (double) done / Math.Max(1, total)),
            token);

        await using var work = _temp.CreateFolder();
        var candidates = await _downloader.ExtractPlugins(downloaded, work.Path,
            (name, done, total) => Report(TranslationStages.Merging, $"Extracting {name}", 0.1 * done / Math.Max(1, total)),
            token);

        var profileResults = new List<ProfileTranslation>();
        var merges = new Dictionary<string, MergeResult>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < instances.Count; i++)
        {
            var instance = instances[i];
            var analysis = analyses.TryGetValue(instance.Profile, out var cached)
                ? cached
                : await Task.Run(() => LoadOrderAnalysis.Build(instance, _logger, token), token);
            var baseFraction = 0.1 + 0.85 * i / instances.Count;
            var modName = TranslationMod(language, perProfileNames ? instance.Profile : null);
            // The language pack replaces the base game strings
            var baseStrings = language.BasePacks.Count > 0 ? supportMod.Combine("Strings") : (AbsolutePath?) null;
            using var mergeJob = await _workLimiter.Begin($"Merging translations for {instance.Profile}", 0, token);
            var mergeBytes = candidates.Sum(c => c.File.FileExists() ? c.File.Size() : 0);
            mergeJob.Size = mergeBytes;
            var mergeReported = 0L;
            var merge = await Task.Run(() => new TranslationMerger(analysis, language,
                instance.Support.LocalizedPatch, _logger, baseStrings).Merge(candidates,
                instance.ModsFolder.Combine(modName, PatchPlugin(language)),
                (done, total) =>
                {
                    var position = mergeBytes * done / Math.Max(1, total);
                    if (position > mergeReported)
                    {
                        mergeJob.ReportNoWait((int) Math.Min(position - mergeReported, int.MaxValue));
                        mergeReported = position;
                    }

                    Report(TranslationStages.Merging,
                        $"Checking translations for {instance.Profile} ({done + 1}/{total})",
                        baseFraction + 0.85 / instances.Count * done / Math.Max(1, total));
                },
                token), token);
            analyses.Remove(instance.Profile);
            merges[instance.Profile] = merge;
            var accepted = merge.Plugins.Where(p => p.Accepted).Select(p => p.Plugin)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count();
            profileResults.Add(new ProfileTranslation(instance.Profile, modName, accepted, merge.StringsSet,
                merge.Outputs.Count));
        }

        Report(TranslationStages.Language, $"Switching the game to {language.DisplayName}", 0);
        var supports = new Dictionary<string, LanguageSupportResult>(StringComparer.OrdinalIgnoreCase);
        var languageFiles = packFiles;
        foreach (var instance in instances)
        {
            var support = await _languageSupport.Build(instance, language, supportMod, IsOwnMod, token);
            languageFiles += support.FilesWritten;
            supports[instance.Profile] = support;
        }

        var voiceArchives = new List<string>();
        var voiceFailed = new List<string>();
        var voicesMod = first.ModsFolder.Combine(VoicesMod(language));
        var looseVoices = false;
        if (request.DownloadVoices && language.Voices is SteamVoices steam)
        {
            var voices = await _voices.Download(first, steam.Code, voicesMod,
                (name, done, total) => Report(TranslationStages.Voices, $"Downloading {name} ({done + 1}/{total})",
                    (double) done / Math.Max(1, total)),
                token);
            voiceArchives.AddRange(voices.Archives);
            voiceFailed.AddRange(voices.Failed);
        }
        else if (request.DownloadVoices && language.Voices is NexusVoices nexusVoices)
        {
            foreach (var pack in nexusVoices.Packs)
            {
                Report(TranslationStages.Voices, $"Installing {pack.Name}", 0.5);
                var installed = await _packs.Install(request.Game, pack, request.DownloadsFolder.Combine("Translations"),
                    voicesMod, manual, token);
                if (installed.Installed) looseVoices = true;
                else voiceFailed.Add(pack.Name);
            }
        }

        Report(TranslationStages.Language, "Updating the profiles", 0.9);
        foreach (var instance in instances)
            await UpdateProfile(instance, language, TranslationMod(language, perProfileNames ? instance.Profile : null),
                merges[instance.Profile].Outputs, supports[instance.Profile], languageFiles > 0, voiceArchives,
                looseVoices);

        var bestPlugins = profileResults.Max(p => p.PluginsTranslated);
        var bestStrings = profileResults.Max(p => p.StringsTranslated);
        foreach (var result in profileResults)
            _logger.LogInformation("Translation to {Language} for {Profile}: {Plugins} plugins, {Strings} strings",
                language.DisplayName, result.Profile, result.PluginsTranslated, result.StringsTranslated);
        _logger.LogInformation(
            "Translation to {Language}: {Found} files found, {Downloaded} downloaded, {Profiles} profiles, {Voices} voice archives",
            language.DisplayName, files.Count, downloaded.Count, profileResults.Count, voiceArchives.Count);
        foreach (var warning in warnings) _logger.LogWarning("{Warning}", warning);
        return new TranslationSummary(files.Count, downloaded.Count, bestPlugins, bestStrings, languageFiles,
            voiceArchives, voiceFailed, merges.Values.SelectMany(m => m.Plugins).ToList(), warnings, profileResults);
    }

    public static bool IsPatchPlugin(string plugin) =>
        Path.GetFileNameWithoutExtension(plugin.TrimStart('*').Trim())
            .StartsWith("WabbajackTranslation_", StringComparison.OrdinalIgnoreCase);

    private static async Task RemovePreviousRun(IReadOnlyList<Mo2Instance> instances)
    {
        foreach (var instance in instances)
        {
            var profile = instance.ProfileFolder;
            var modlist = await ProfileText.Load(profile.Combine("modlist.txt"));
            modlist.Lines.RemoveAll(l => l.Length > 1 && (l[0] == '+' || l[0] == '-') && IsOwnMod(l[1..]));
            await modlist.Save(profile.Combine("modlist.txt"));

            foreach (var name in new[] {"plugins.txt", "loadorder.txt"})
            {
                var text = await ProfileText.Load(profile.Combine(name));
                text.Lines.RemoveAll(l => !l.StartsWith('#') && IsPatchPlugin(l));
                await text.Save(profile.Combine(name));
            }
        }

        var mods = instances[0].ModsFolder;
        if (!mods.DirectoryExists()) return;
        foreach (var folder in mods.EnumerateDirectories(recursive: false))
            if (IsOwnMod(folder.FileName.ToString()))
                folder.DeleteDirectory();
    }

    private async Task UpdateProfile(Mo2Instance instance, GameLanguage language, string translationMod,
        IReadOnlyList<AbsolutePath> patches, LanguageSupportResult support, bool haveLanguageFiles,
        IReadOnlyList<string> voiceArchives, bool looseVoices)
    {
        var profile = instance.ProfileFolder;
        var game = instance.Support;
        var iniPath = profile.Combine(game.IniFile);
        if (iniPath.FileExists())
        {
            var ini = await ProfileText.Load(iniPath);
            ini.SetIniValue("General", "sLanguage", language.SLanguage);
            var wantedKeys = language.IniSettings.Select(s => s.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var setting in game.Languages.SelectMany(l => l.IniSettings))
                if (!wantedKeys.Contains(setting.Key))
                    ini.RemoveIniValue(setting.Key);
            foreach (var setting in language.IniSettings)
                ini.SetIniValue(setting.Section, setting.Key, setting.Value);
            var steamCodes = game.Languages.Select(l => l.Voices).OfType<SteamVoices>().Select(v => v.Code);
            var languageVoices = steamCodes.Select(game.VoicesArchive).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var baseVoices = language.Voices is SteamVoices mine ? game.VoicesArchive(mine.Code) : null;
            var useBaseVoices = baseVoices != null && voiceArchives.Contains(baseVoices, StringComparer.OrdinalIgnoreCase);
            ini.EditIniList("Archive", game.BaseVoicesIniKey, l => l
                .Select(a => languageVoices.Contains(a) ? game.EnglishBaseVoices : a)
                .Select(a => useBaseVoices && a.Equals(game.EnglishBaseVoices, StringComparison.OrdinalIgnoreCase)
                    ? baseVoices!
                    : a)
                .ToList());

            if (game.EnglishPluginVoicesPattern != null)
            {
                var replaced = voiceArchives
                    .Where(a => a.Contains(" - Voices_", StringComparison.OrdinalIgnoreCase))
                    .Select(a => a[..a.IndexOf(" - Voices_", StringComparison.OrdinalIgnoreCase)])
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var english = support.EnglishVoiceArchives
                    .Where(a => !replaced.Contains(a[..a.IndexOf(" - Voices_", StringComparison.OrdinalIgnoreCase)]))
                    .ToList();
                ini.EditIniList("Archive", game.EnglishPluginVoicesIniKey, l =>
                    l.Where(a => !support.EnglishVoiceArchives.Contains(a, StringComparer.OrdinalIgnoreCase))
                        .Concat(english).ToList());
            }

            await ini.Save(iniPath);
        }
        else
        {
            _logger.LogWarning("Profile {Profile} has no {Ini} of its own, so the game language was not changed",
                instance.Profile, game.IniFile);
        }

        var modlist = await ProfileText.Load(profile.Combine("modlist.txt"));
        if (voiceArchives.Count > 0 || looseVoices) modlist.SetModEnabledAtTop(VoicesMod(language));
        if (haveLanguageFiles) modlist.SetModEnabledAtTop(LanguageSupportMod(language));
        if (patches.Count > 0) modlist.SetModEnabledAtTop(translationMod);
        await modlist.Save(profile.Combine("modlist.txt"));

        if (patches.Count == 0) return;
        var plugins = await ProfileText.Load(profile.Combine("plugins.txt"));
        var loadOrder = await ProfileText.Load(profile.Combine("loadorder.txt"));
        foreach (var patch in patches)
        {
            plugins.SetPluginLast(patch.FileName.ToString(), true);
            loadOrder.SetPluginLast(patch.FileName.ToString(), false);
        }

        await plugins.Save(profile.Combine("plugins.txt"));
        await loadOrder.Save(profile.Combine("loadorder.txt"));
    }
}
