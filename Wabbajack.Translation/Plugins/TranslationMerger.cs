using Microsoft.Extensions.Logging;
using Mutagen.Bethesda.Plugins;
using Wabbajack.Paths;

namespace Wabbajack.Translation.Plugins;

public sealed record TranslationCandidate(string Plugin, AbsolutePath File, string Source);

public sealed record PluginTranslationReport(
    string Plugin,
    string Source,
    bool Accepted,
    int MatchedFields,
    int DifferingFields,
    int VisibleFields,
    int AppliedFields,
    int OverriddenLater,
    long VisibleChars,
    long AppliedChars);

public sealed record MergeResult(
    IReadOnlyList<PluginTranslationReport> Plugins,
    int RecordsOverridden,
    int StringsSet,
    int StringsFailed,
    int Masters,
    IReadOnlyList<AbsolutePath> Outputs);

public sealed class TranslationMerger
{
    private readonly LoadOrderAnalysis _analysis;
    private readonly GameLanguage _language;
    private readonly bool _localizedPatch;
    private readonly AbsolutePath? _baseStringsFolder;
    private readonly ILogger _logger;

    public TranslationMerger(LoadOrderAnalysis analysis, GameLanguage language, bool localizedPatch, ILogger logger,
        AbsolutePath? baseStringsFolder = null)
    {
        _baseStringsFolder = baseStringsFolder;
        _analysis = analysis;
        _language = language;
        _localizedPatch = localizedPatch;
        _logger = logger;
    }

    public MergeResult Merge(IReadOnlyList<TranslationCandidate> candidates, AbsolutePath output,
        Action<int, int>? progress, CancellationToken token)
    {
        var reports = new List<PluginTranslationReport>();
        var edits = new List<PatchEdit>();
        var groups = candidates.GroupBy(c => c.Plugin, StringComparer.OrdinalIgnoreCase).ToList();
        var done = 0;

        foreach (var group in groups)
        {
            token.ThrowIfCancellationRequested();
            progress?.Invoke(done++, groups.Count);
            var index = _analysis.IndexOf(group.Key);
            if (index < 0) continue;

            var owner = LoadOrderAnalysis.Strings(_analysis.Mods[index]);
            (PluginTranslationReport Report, List<PatchEdit> Edits)? best = null;
            foreach (var candidate in group)
            {
                var evaluated = Evaluate(index, owner, candidate);
                if (evaluated == null) continue;
                reports.Add(evaluated.Value.Report);
                // many translations of one mod sokeep the one that applies the most text
                if (evaluated.Value.Report.Accepted &&
                    (best == null || evaluated.Value.Report.AppliedChars > best.Value.Report.AppliedChars))
                    best = evaluated;
            }

            if (best != null) edits.AddRange(best.Value.Edits);
        }

        if (edits.Count == 0)
            return new MergeResult(reports, 0, 0, 0, 0, []);

        var readable = _analysis.Plugins.Where(p => !_analysis.Unreadable.Contains(p.Key.FileName)).ToList();
        Mutagen.Bethesda.Strings.Language? localized = _localizedPatch ? _language.MutagenLanguage : null;
        var written = _analysis.Engine.Write(readable, edits, _analysis.Plugins.Select(p => p.Key).ToList(),
            _analysis.Styles, output, _language.MutagenLanguage, _baseStringsFolder, localized, token);
        return new MergeResult(reports, written.Records, written.Set, written.Failed, written.Masters, written.Outputs);
    }

    private (PluginTranslationReport Report, List<PatchEdit> Edits)? Evaluate(
        int index, Dictionary<(FormKey, string), string> owner, TranslationCandidate candidate)
    {
        Dictionary<(FormKey, string), string> translated;
        try
        {
            var mod = _analysis.OpenTranslation(_analysis.Plugins[index].Key, candidate.File, _language);
            translated = LoadOrderAnalysis.Strings(mod);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Could not read translation {File} for {Plugin}: {Message}", candidate.File,
                candidate.Plugin, ex.GetBaseException().Message);
            return null;
        }

        var matched = 0;
        var differing = 0;
        foreach (var (key, text) in owner)
        {
            if (!translated.TryGetValue(key, out var other)) continue;
            matched++;
            if (other != text) differing++;
        }

        var accepted = TranslationRules.IsTranslationOf(matched, differing);
        var edits = new List<PatchEdit>();
        int visible = 0, applied = 0, overridden = 0;
        long visibleChars = 0, appliedChars = 0;
        foreach (var formKey in owner.Keys.Select(k => k.Item1).Distinct())
        {
            if (!_analysis.Winning.TryGetValue(formKey, out var record)) continue;
            foreach (var (path, text) in record.Strings)
            {
                if (!TranslationRules.IsTranslatable(text)) continue;
                if (!owner.TryGetValue((formKey, path), out var ownerText)) continue;
                translated.TryGetValue((formKey, path), out var translatedText);
                switch (TranslationRules.Decide(text!, ownerText, translatedText))
                {
                    case FieldDecision.OverriddenLater:
                        overridden++;
                        continue;
                    case FieldDecision.Apply:
                        visible++;
                        visibleChars += text!.Length;
                        applied++;
                        appliedChars += text.Length;
                        if (accepted) edits.Add(new PatchEdit(formKey, record.Plugin, record.Type, path, translatedText!));
                        break;
                    default:
                        visible++;
                        visibleChars += text!.Length;
                        break;
                }
            }
        }

        var report = new PluginTranslationReport(candidate.Plugin, candidate.Source, accepted, matched, differing,
            visible, applied, overridden, visibleChars, appliedChars);
        return (report, edits);
    }
}
