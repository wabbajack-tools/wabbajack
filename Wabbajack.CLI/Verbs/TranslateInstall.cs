using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.CLI.Builder;
using Wabbajack.DTOs;
using Wabbajack.Paths;
using Wabbajack.Translation;

namespace Wabbajack.CLI.Verbs;

public class TranslateInstall
{
    private readonly ILogger<TranslateInstall> _logger;
    private readonly TranslationRunner _runner;

    public TranslateInstall(ILogger<TranslateInstall> logger, TranslationRunner runner)
    {
        _logger = logger;
        _runner = runner;
    }

    public static VerbDefinition Definition = new VerbDefinition("translate-install",
        "Finds translation patches on Nexus Mods for an installed modlist, merges them and switches the game language",
        new[]
        {
            new OptionDefinition(typeof(AbsolutePath), "i", "install", "Modlist install folder"),
            new OptionDefinition(typeof(AbsolutePath), "d", "downloads", "Downloads folder"),
            new OptionDefinition(typeof(string), "l", "language",
                "Language: " + string.Join(", ", TranslationLanguages.All.Select(l => l.Id))),
            new OptionDefinition(typeof(bool), "v", "voices", "Also download the language's voice files from Steam"),
            new OptionDefinition(typeof(string), "p", "profile", "MO2 profile to translate, instead of the selected one"),
            new OptionDefinition(typeof(string), "g", "game", "Game, read from ModOrganizer.ini when not given")
        });

    public async Task<int> Run(AbsolutePath install, AbsolutePath downloads, string language, bool voices,
        string profile, string game, CancellationToken token)
    {
        Game gameType;
        if (string.IsNullOrWhiteSpace(game))
        {
            var ini = await ProfileText.Load(install.Combine("ModOrganizer.ini"));
            var name = (ini.GetIniValue("gameName") ?? "").Trim();
            if (name.Equals("Skyrim Special Edition", StringComparison.OrdinalIgnoreCase))
                gameType = Game.SkyrimSpecialEdition;
            else if (name.Equals("Fallout 4", StringComparison.OrdinalIgnoreCase))
                gameType = Game.Fallout4;
            else
            {
                _logger.LogError("Translation supports Skyrim Special Edition and Fallout 4 lists only, this one is {Game}",
                    name.Length > 0 ? name : "an unknown game");
                return 1;
            }
        }
        else if (!Enum.TryParse(game, true, out gameType))
        {
            _logger.LogError("Unknown game {Game}", game);
            return 1;
        }

        var support = TranslationGames.For(gameType);
        var lang = support?.Find(language ?? "");
        if (support == null || lang == null)
        {
            _logger.LogError("{Game} has no translation language {Language}; choose from {Languages}", gameType,
                language, string.Join(", ", support?.Languages.Select(l => l.Id) ?? []));
            return 1;
        }

        var progress = new Progress<TranslationProgress>(p =>
            _logger.LogInformation("[{Stage}] {Text} ({Percent:P0})", p.Stage, p.Text, p.Fraction));
        var summary = await _runner.Run(
            new TranslationRequest(install, downloads, gameType, install.Combine("Stock Game"), lang, voices,
                string.IsNullOrWhiteSpace(profile) ? null : profile),
            null, progress, token);

        foreach (var report in summary.Reports.OrderByDescending(r => r.AppliedChars))
            Console.WriteLine(
                $"{(report.Accepted ? "OK  " : "SKIP")} {report.Plugin,-50} {report.AppliedFields,7} of {report.VisibleFields,7} fields  {report.Source}");
        Console.WriteLine(
            $"{summary.TranslationFilesFound} files found, {summary.TranslationFilesDownloaded} downloaded, {summary.PluginsTranslated} plugins translated, {summary.StringsTranslated} strings, {summary.LanguageFilesWritten} language files, voices: {string.Join(", ", summary.VoiceArchives)}");
        foreach (var result in summary.Profiles)
            Console.WriteLine(
                $"Profile {result.Profile}: {result.PluginsTranslated} plugins, {result.StringsTranslated} strings, {result.PatchFiles} patch files in {result.Mod}");
        foreach (var warning in summary.Warnings)
            Console.WriteLine("Warning: " + warning);
        if (summary.VoiceArchivesFailed.Count > 0)
            Console.WriteLine("Voice archives that failed: " + string.Join(", ", summary.VoiceArchivesFailed));
        return 0;
    }
}
