using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Strings;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Translation.Plugins;

public sealed record PluginSource(ModKey Key, AbsolutePath Path, AbsolutePath Folder, int CodePage, bool IsBase);

public sealed record WinningRecord(int Plugin, string Type, IReadOnlyList<(string Path, string? Text)> Strings);

public sealed class LoadOrderAnalysis
{
    private const int Western = 1252;

    private LoadOrderAnalysis(IGameEngine engine, IReadOnlyList<PluginSource> plugins, IModGetter[] mods,
        KeyedMasterStyle[] styles, IReadOnlyDictionary<FormKey, WinningRecord> winning,
        IReadOnlySet<string> unreadable, IReadOnlyDictionary<string, long> ownedChars)
    {
        Engine = engine;
        Plugins = plugins;
        Mods = mods;
        Styles = styles;
        Winning = winning;
        Unreadable = unreadable;
        OwnedChars = ownedChars;
    }

    public IGameEngine Engine { get; }
    public IReadOnlyList<PluginSource> Plugins { get; }
    public IModGetter[] Mods { get; }
    public KeyedMasterStyle[] Styles { get; }
    public IReadOnlyDictionary<FormKey, WinningRecord> Winning { get; }
    public IReadOnlySet<string> Unreadable { get; }
    public IReadOnlyDictionary<string, long> OwnedChars { get; }

    public int IndexOf(string plugin) =>
        Plugins.ToList().FindIndex(p => p.Key.FileName.String.Equals(plugin, StringComparison.OrdinalIgnoreCase));

    public static LoadOrderAnalysis Build(Mo2Instance instance, ILogger logger, CancellationToken token)
    {
        var engine = GameEngines.For(instance.Support.Release);
        var located = new List<PluginSource>();
        foreach (var plugin in instance.ActivePlugins)
        {
            var path = instance.DataRootsByPriority().Select(r => r.Combine(plugin)).FirstOrDefault(p => p.FileExists());
            if (path == default)
            {
                logger.LogWarning("Plugin {Plugin} is in the load order but no file was found", plugin);
                continue;
            }

            located.Add(new PluginSource(ModKey.FromFileName(plugin), path, path.Parent, Western,
                instance.BasePlugins.Contains(plugin)));
        }

        var styles = located
            .Select(p => KeyedMasterStyle.FromPath(new ModPath(p.Key, p.Path.ToString()), instance.Support.Release))
            .ToArray();

        var mods = new IModGetter[located.Count];
        var sources = located.ToArray();
        Parallel.For(0, located.Count, new ParallelOptions {CancellationToken = token}, i =>
        {
            var p = located[i];
            if (p.IsBase)
            {
                mods[i] = engine.Open(p.Key, p.Path, p.Folder, styles, Western, Language.English);
                return;
            }

            var (mod, codePage) = OpenDetected(engine, p.Key, p.Path, p.Folder, styles, [Western], Language.English);
            mods[i] = mod;
            sources[i] = p with {CodePage = codePage};
        });

        var unreadable = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        var formsPerMod = new HashSet<FormKey>[mods.Length];
        Parallel.For(0, mods.Length, new ParallelOptions {CancellationToken = token}, i =>
        {
            var set = new HashSet<FormKey>();
            try
            {
                foreach (var record in mods[i].EnumerateMajorRecords())
                    if (!RecordStrings.IsSkippedRecord(record))
                        set.Add(record.FormKey);
            }
            catch (Exception ex)
            {
                unreadable[sources[i].Key.FileName] = 0;
                logger.LogInformation("Translation skips records it could not read in {Plugin}: {Message}",
                    sources[i].Key.FileName, ex.GetBaseException().Message);
            }

            formsPerMod[i] = set;
        });

        var winner = new Dictionary<FormKey, int>();
        for (var i = mods.Length - 1; i >= 0; i--)
            foreach (var formKey in formsPerMod[i])
                winner.TryAdd(formKey, i);

        var wanted = winner.Where(kv => !sources[kv.Value].IsBase)
            .GroupBy(kv => kv.Value).ToDictionary(g => g.Key, g => g.Select(kv => kv.Key).ToHashSet());

        var winning = new ConcurrentDictionary<FormKey, WinningRecord>();
        Parallel.For(0, mods.Length, new ParallelOptions {CancellationToken = token}, i =>
        {
            if (!wanted.TryGetValue(i, out var keys)) return;
            try
            {
                foreach (var record in mods[i].EnumerateMajorRecords())
                {
                    if (!keys.Contains(record.FormKey) || RecordStrings.IsSkippedRecord(record)) continue;
                    List<(string, string?)> strings;
                    try
                    {
                        strings = RecordStrings.Extract(record);
                    }
                    catch
                    {
                        continue;
                    }

                    if (strings.Count > 0)
                        winning[record.FormKey] = new WinningRecord(i, RecordStrings.RecordTypeName(record), strings);
                }
            }
            catch (Exception ex)
            {
                unreadable[sources[i].Key.FileName] = 0;
                logger.LogInformation("Translation could not read strings from {Plugin}: {Message}",
                    sources[i].Key.FileName, ex.GetBaseException().Message);
            }
        });

        var owned = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var baseKeys = sources.Where(s => s.IsBase).Select(s => s.Key).ToHashSet();
        foreach (var (formKey, record) in winning)
        {
            var owner = baseKeys.Contains(formKey.ModKey)
                ? sources[record.Plugin].Key.FileName.String
                : formKey.ModKey.FileName.String;
            foreach (var (_, text) in record.Strings)
                if (TranslationRules.IsTranslatable(text))
                    owned[owner] = owned.GetValueOrDefault(owner) + text!.Length;
        }

        return new LoadOrderAnalysis(engine, sources, mods, styles, winning,
            unreadable.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase), owned);
    }

    public IModGetter OpenTranslation(ModKey key, AbsolutePath path, GameLanguage language)
    {
        // Translated localized plugins often do strings under English or Chinese names rather than the target language.
        var languages = new[] {language.MutagenLanguage, Language.English, Language.Chinese}.Distinct().ToList();
        IModGetter? fallback = null;
        foreach (var target in languages)
        {
            var (mod, _) = OpenDetected(Engine, key, path, path.Parent, Styles, language.LegacyCodePages, target);
            fallback ??= mod;
            if (!IsLocalized(mod) || Strings(mod).Count > 0) return mod;
        }

        return fallback!;
    }

    private static bool IsLocalized(IModGetter mod) => mod.UsingLocalization;

    private static (IModGetter Mod, int CodePage) OpenDetected(IGameEngine engine, ModKey key, AbsolutePath path,
        AbsolutePath folder, KeyedMasterStyle[] styles, IReadOnlyList<int> legacyCodePages, Language language)
    {
        // Non-localized plugins may be UTF-8 or a legacy code page. Replacement characters mean it was not UTF-8
        var utf8 = engine.Open(key, path, folder, styles, GameEngines.Utf8, language);
        if (IsLocalized(utf8) || !HasReplacementCharacters(utf8)) return (utf8, GameEngines.Utf8);
        var legacy = legacyCodePages.FirstOrDefault(Western);
        return (engine.Open(key, path, folder, styles, legacy, language), legacy);
    }

    private static bool HasReplacementCharacters(IModGetter mod)
    {
        try
        {
            foreach (var record in mod.EnumerateMajorRecords())
            {
                if (RecordStrings.IsSkippedRecord(record)) continue;
                List<(string Path, string? Text)> strings;
                try
                {
                    strings = RecordStrings.Extract(record);
                }
                catch
                {
                    continue;
                }

                if (strings.Any(s => s.Text != null && s.Text.Contains('�'))) return true;
            }
        }
        catch
        {
        }

        return false;
    }

    public static Dictionary<(FormKey, string), string> Strings(IModGetter mod)
    {
        var result = new Dictionary<(FormKey, string), string>();
        try
        {
            foreach (var record in mod.EnumerateMajorRecords())
            {
                if (RecordStrings.IsSkippedRecord(record)) continue;
                List<(string Path, string? Text)> strings;
                try
                {
                    strings = RecordStrings.Extract(record);
                }
                catch
                {
                    continue;
                }

                foreach (var (path, text) in strings)
                    if (!string.IsNullOrWhiteSpace(text))
                        result.TryAdd((record.FormKey, path), text!);
            }
        }
        catch
        {
        }

        return result;
    }
}
