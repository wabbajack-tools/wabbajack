using System.Text;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Plugins.Cache.Internals.Implementations;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Strings;
using Mutagen.Bethesda.Strings.DI;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Translation.Plugins;

public sealed record PatchEdit(FormKey FormKey, int Plugin, string Type, string Path, string Text);

public sealed record PatchWriteResult(int Records, int Set, int Failed, int Masters, IReadOnlyList<AbsolutePath> Outputs);

public interface IGameEngine
{
    GameRelease Release { get; }

    IModGetter Open(ModKey key, AbsolutePath path, AbsolutePath folder, KeyedMasterStyle[] styles, int codePage,
        Language language);

    PatchWriteResult Write(IReadOnlyList<PluginSource> readable, IReadOnlyList<PatchEdit> edits,
        IReadOnlyList<ModKey> pluginKeys, KeyedMasterStyle[] styles, AbsolutePath output, Language readLanguage,
        AbsolutePath? baseStringsFolder, Language? localized, CancellationToken token);
}

public static class GameEngines
{
    public const int Utf8 = 65001;

    public static IGameEngine For(GameRelease release) => release switch
    {
        GameRelease.Fallout4 => new Fallout4Engine(),
        GameRelease.SkyrimSE => new SkyrimEngine(),
        _ => throw new NotSupportedException($"No translation engine for {release}")
    };

    public static IMutagenEncoding Encoding(int codePage) =>
        codePage == Utf8
            ? MutagenEncoding._utf8
            : new MutagenEncodingWrapper(CodePagesEncodingProvider.Instance.GetEncoding(codePage)!);

    public static IReadOnlyList<AbsolutePath> SplitParts(AbsolutePath output)
    {
        if (!output.Parent.DirectoryExists()) return [];
        var stem = output.FileName.WithoutExtension().ToString();
        var extension = output.Extension.ToString();
        return output.Parent.EnumerateFiles("*" + extension, recursive: false)
            .Select(f => (File: f, Index: PartIndex(f.FileName.WithoutExtension().ToString(), stem)))
            .Where(x => x.Index > 0)
            .OrderBy(x => x.Index)
            .Select(x => x.File)
            .ToList();
    }

    private static int PartIndex(string name, string stem)
    {
        if (name.Equals(stem, StringComparison.OrdinalIgnoreCase)) return 1;
        if (!name.StartsWith(stem + "_", StringComparison.OrdinalIgnoreCase)) return 0;
        return int.TryParse(name[(stem.Length + 1)..], out var index) && index > 1 ? index : 0;
    }
}

public abstract class MutagenGameEngine<TMod, TModGetter> : IGameEngine
    where TMod : class, IContextMod<TMod, TModGetter>, TModGetter
    where TModGetter : class, IContextGetterMod<TMod, TModGetter>
{
    public abstract GameRelease Release { get; }

    protected abstract TModGetter OpenMod(ModPath path, AbsolutePath folder, KeyedMasterStyle[] styles,
        int codePage, Language language, ILinkCache? linkCache = null, AbsolutePath? stringsFolder = null);

    protected abstract TMod CreatePatch(ModKey key, bool localized);

    protected abstract void WritePatch(TMod patch, AbsolutePath output, KeyedMasterStyle[] styles, bool localized);

    public IModGetter Open(ModKey key, AbsolutePath path, AbsolutePath folder, KeyedMasterStyle[] styles,
        int codePage, Language language) =>
        OpenMod(new ModPath(key, path.ToString()), folder, styles, codePage, language);

    public PatchWriteResult Write(IReadOnlyList<PluginSource> readable, IReadOnlyList<PatchEdit> edits,
        IReadOnlyList<ModKey> pluginKeys, KeyedMasterStyle[] styles, AbsolutePath output, Language readLanguage,
        AbsolutePath? baseStringsFolder, Language? localized, CancellationToken token)
    {
        var patchKey = ModKey.FromFileName(output.FileName.ToString());
        var patch = CreatePatch(patchKey, localized != null);
        var typeCache = new ImmutableLoadOrderLinkCache<TMod, TModGetter>(
            readable.Select(p => OpenMod(new ModPath(p.Key, p.Path.ToString()), p.Folder, styles, p.CodePage,
                Language.English)).ToList(),
            LinkCachePreferences.Default);
        // Second cache reads the target language, so parent records copied into the patch carry translated text.
        var cache = new ImmutableLoadOrderLinkCache<TMod, TModGetter>(
            readable.Select(p => OpenMod(new ModPath(p.Key, p.Path.ToString()), p.Folder, styles, p.CodePage,
                readLanguage, typeCache, p.IsBase ? baseStringsFolder : null)).ToList(),
            LinkCachePreferences.Default);
        var assembly = typeof(TModGetter).Assembly;
        var ns = typeof(TModGetter).Namespace;
        var language = localized ?? Language.English;

        int records = 0, set = 0, failed = 0;
        foreach (var group in edits.GroupBy(e => e.FormKey))
        {
            token.ThrowIfCancellationRequested();
            var first = group.First();
            var type = assembly.GetType($"{ns}.I{first.Type}Getter");
            if (type == null || !cache.TryResolveContext(group.Key, type, out var context) ||
                context.ModKey != pluginKeys[first.Plugin])
            {
                failed += group.Count();
                continue;
            }

            var record = context.GetOrAddAsOverride(patch);
            records++;
            foreach (var edit in group)
                if (RecordStrings.Set(record, edit.Path, edit.Text, language)) set++;
                else failed++;
        }
        // Parent records such as DIAL come along with their children, so localize every record, not just the edited ones
        if (localized != null)
            foreach (var record in patch.EnumerateMajorRecords())
                RecordStrings.Localize(record, language);

        output.Parent.CreateDirectory();
        foreach (var old in GameEngines.SplitParts(output)) old.Delete();
        var patchStyles = styles.Append(new KeyedMasterStyle(patchKey, MasterStyle.Small)).ToArray();
        WritePatch(patch, output, patchStyles, localized != null);

        var outputs = GameEngines.SplitParts(output);
        var masters = outputs.Sum(o => OpenMod(new ModPath(ModKey.FromFileName(o.FileName.ToString()), o.ToString()),
            o.Parent, patchStyles, GameEngines.Utf8, language).MasterReferences.Count);
        return new PatchWriteResult(records, set, failed, masters, outputs);
    }
}

public sealed class Fallout4Engine : MutagenGameEngine<IFallout4Mod, IFallout4ModGetter>
{
    public override GameRelease Release => GameRelease.Fallout4;

    protected override IFallout4ModGetter OpenMod(ModPath path, AbsolutePath folder, KeyedMasterStyle[] styles,
        int codePage, Language language, ILinkCache? linkCache = null, AbsolutePath? stringsFolder = null)
    {
        var builder = Fallout4Mod.Create(Fallout4Release.Fallout4)
            .FromPath(path)
            .WithKnownMasters(styles)
            .WithDataFolder(folder.ToString())
            .WithTargetLanguage(language)
            .WithLinkCache(linkCache);
        if (stringsFolder is { } strings && strings.DirectoryExists())
            builder = builder.WithStringsFolder(strings.ToString());
        return (codePage == GameEngines.Utf8
            ? builder.WithUtf8Encoding()
            : builder.WithNonLocalizedEncoding(GameEngines.Encoding(codePage))).Construct();
    }

    protected override IFallout4Mod CreatePatch(ModKey key, bool localized)
    {
        var patch = new Fallout4Mod(key, Fallout4Release.Fallout4);
        patch.ModHeader.Flags |= Fallout4ModHeader.HeaderFlag.Small;
        if (localized) patch.ModHeader.Flags |= Fallout4ModHeader.HeaderFlag.Localized;
        return patch;
    }

    protected override void WritePatch(IFallout4Mod patch, AbsolutePath output, KeyedMasterStyle[] styles,
        bool localized)
    {
        // Splits into further plugins past the master limit.
        var builder = ((Fallout4Mod) patch).BeginWrite.ToPath(output.ToString()).WithLoadOrder(styles);
        (localized ? builder : builder.WithUtf8Encoding()).WithAutoSplit().WriteAsync().GetAwaiter().GetResult();
    }
}

public sealed class SkyrimEngine : MutagenGameEngine<ISkyrimMod, ISkyrimModGetter>
{
    public override GameRelease Release => GameRelease.SkyrimSE;

    protected override ISkyrimModGetter OpenMod(ModPath path, AbsolutePath folder, KeyedMasterStyle[] styles,
        int codePage, Language language, ILinkCache? linkCache = null, AbsolutePath? stringsFolder = null)
    {
        var builder = SkyrimMod.Create(SkyrimRelease.SkyrimSE)
            .FromPath(path)
            .WithKnownMasters(styles)
            .WithDataFolder(folder.ToString())
            .WithTargetLanguage(language)
            .WithLinkCache(linkCache);
        if (stringsFolder is { } strings && strings.DirectoryExists())
            builder = builder.WithStringsFolder(strings.ToString());
        return (codePage == GameEngines.Utf8
            ? builder.WithUtf8Encoding()
            : builder.WithNonLocalizedEncoding(GameEngines.Encoding(codePage))).Construct();
    }

    protected override ISkyrimMod CreatePatch(ModKey key, bool localized)
    {
        var patch = new SkyrimMod(key, SkyrimRelease.SkyrimSE);
        patch.ModHeader.Flags |= SkyrimModHeader.HeaderFlag.Small;
        if (localized) patch.ModHeader.Flags |= SkyrimModHeader.HeaderFlag.Localized;
        return patch;
    }

    protected override void WritePatch(ISkyrimMod patch, AbsolutePath output, KeyedMasterStyle[] styles,
        bool localized)
    {
        var builder = ((SkyrimMod) patch).BeginWrite.ToPath(output.ToString()).WithLoadOrder(styles);
        (localized ? builder : builder.WithUtf8Encoding()).WithAutoSplit().WriteAsync().GetAwaiter().GetResult();
    }
}
