using Wabbajack.DTOs;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Translation;

public sealed class Mo2Instance
{
    private Mo2Instance(GameTranslationSupport support, AbsolutePath root, string profile, AbsolutePath gameFolder,
        IReadOnlyList<string> enabledMods, IReadOnlyList<string> plugins, IReadOnlySet<string> basePlugins)
    {
        Support = support;
        Root = root;
        Profile = profile;
        GameFolder = gameFolder;
        EnabledModsByPriority = enabledMods;
        ActivePlugins = plugins;
        BasePlugins = basePlugins;
    }

    public GameTranslationSupport Support { get; }
    public AbsolutePath Root { get; }
    public string Profile { get; }
    public AbsolutePath GameFolder { get; }
    public AbsolutePath DataFolder => GameFolder.Combine("Data");
    public AbsolutePath ProfileFolder => Root.Combine("profiles", Profile);
    public AbsolutePath ModsFolder => Root.Combine("mods");
    public AbsolutePath OverwriteFolder => Root.Combine("overwrite");
    public IReadOnlyList<string> EnabledModsByPriority { get; }
    public IReadOnlyList<string> ActivePlugins { get; }
    public IReadOnlySet<string> BasePlugins { get; }

    public IEnumerable<AbsolutePath> DataRootsByPriority()
    {
        yield return OverwriteFolder;
        foreach (var mod in EnabledModsByPriority)
            yield return ModsFolder.Combine(mod);
        yield return DataFolder;
    }

    public AbsolutePath? Locate(string relativePath)
    {
        foreach (var root in DataRootsByPriority())
        {
            var candidate = root.Combine(relativePath);
            if (candidate.FileExists()) return candidate;
        }

        return null;
    }

    public static IReadOnlyList<string> ProfileNames(AbsolutePath root)
    {
        var profiles = root.Combine("profiles");
        if (!profiles.DirectoryExists()) return [];
        return profiles.EnumerateDirectories(recursive: false)
            .Where(d => d.Combine("modlist.txt").FileExists() && d.Combine("plugins.txt").FileExists())
            .Select(d => d.FileName.ToString())
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static async Task<Mo2Instance> Load(AbsolutePath root, Game game, AbsolutePath fallbackGameFolder,
        string? profileOverride = null)
    {
        var support = TranslationGames.For(game) ??
                      throw new NotSupportedException($"Translation is not supported for {game} yet.");

        var ini = await ProfileText.Load(root.Combine("ModOrganizer.ini"));
        var profile = profileOverride ?? Unwrap(ini.GetIniValue("selected_profile"));
        if (string.IsNullOrWhiteSpace(profile))
            profile = root.Combine("profiles").EnumerateDirectories(recursive: false)
                .Select(d => d.FileName.ToString()).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(profile))
            throw new InvalidOperationException($"No MO2 profile found under {root}.");

        var gamePathText = Unwrap(ini.GetIniValue("gamePath"))?.Replace(@"\\", @"\");
        var gameFolder = !string.IsNullOrWhiteSpace(gamePathText) && gamePathText.ToAbsolutePath().DirectoryExists()
            ? gamePathText.ToAbsolutePath()
            : fallbackGameFolder;

        var profileFolder = root.Combine("profiles", profile);
        var modlist = await ProfileText.Load(profileFolder.Combine("modlist.txt"));
        var enabled = modlist.Lines.Where(l => l.StartsWith('+')).Select(l => l[1..]).ToList();

        var pluginsTxt = await ProfileText.Load(profileFolder.Combine("plugins.txt"));
        var active = pluginsTxt.Lines.Where(l => l.StartsWith('*')).Select(l => l[1..].Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var dataFolder = gameFolder.Combine("Data");
        var basePlugins = support.ImplicitPlugins.Where(p => dataFolder.Combine(p).FileExists())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var roots = new[] {root.Combine("overwrite")}
            .Concat(enabled.Select(m => root.Combine("mods", m)))
            .Append(dataFolder)
            .ToList();
        // CC plugins are active through the .ccc file, even when installed as mods
        var ccc = gameFolder.Combine(support.CccFile);
        if (ccc.FileExists())
            foreach (var line in await File.ReadAllLinesAsync(ccc.ToString()))
                if (line.Trim().Length > 0 && roots.Any(r => r.Combine(line.Trim()).FileExists()))
                    basePlugins.Add(line.Trim());

        var loadOrder = await ProfileText.Load(profileFolder.Combine("loadorder.txt"));
        var ordered = loadOrder.Lines.Where(l => !l.StartsWith('#') && l.Trim().Length > 0).Select(l => l.Trim())
            .Where(l => active.Contains(l) || basePlugins.Contains(l)).ToList();
        ordered.InsertRange(0, support.ImplicitPlugins.Where(p => basePlugins.Contains(p) &&
                                                                 !ordered.Contains(p, StringComparer.OrdinalIgnoreCase)));
        ordered.AddRange(basePlugins.Where(b => !ordered.Contains(b, StringComparer.OrdinalIgnoreCase)));
        foreach (var plugin in active.Where(a => !ordered.Contains(a, StringComparer.OrdinalIgnoreCase)))
            ordered.Add(plugin);

        return new Mo2Instance(support, root, profile, gameFolder, enabled, ordered, basePlugins);
    }

    private static string? Unwrap(string? value)
    {
        if (value == null) return null;
        return value.StartsWith("@ByteArray(") && value.EndsWith(')') ? value[11..^1] : value;
    }
}
