using Microsoft.Extensions.Logging.Abstractions;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Wabbajack.DTOs;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Strings;
using Wabbajack.Translation.Plugins;
using Xunit;

namespace Wabbajack.Translation.Test;

public class TranslationMergerTests : IDisposable
{
    private static readonly ModKey A = ModKey.FromFileName("A.esp");
    private static readonly ModKey B = ModKey.FromFileName("B.esp");
    private static readonly GameLanguage German = TranslationGames.Fallout4.Find("german")!;
    private readonly AbsolutePath _root;

    public TranslationMergerTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "wj-translation-" + Guid.NewGuid().ToString("N")).ToAbsolutePath();
        _root.CreateDirectory();
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root.ToString(), true);
        }
        catch
        {
        }
    }

    [Fact]
    public async Task KeepsLaterOverridesAndTranslatesOnlyTheOwnersText()
    {
        var modA = new Fallout4Mod(A, Fallout4Release.Fallout4);
        var sword = modA.Weapons.AddNew("Sword");
        sword.Name = "Iron Sword";
        sword.Weight = 10;
        var axe = modA.Weapons.AddNew("Axe");
        axe.Name = "Iron Axe";
        axe.Weight = 12;
        var club = modA.Weapons.AddNew("Club");
        club.Name = "Club";
        club.Weight = 5;
        await Write(modA, _root.Combine("mods", "A", "A.esp"));

        var modB = new Fallout4Mod(B, Fallout4Release.Fallout4);
        var swordB = modB.Weapons.GetOrAddAsOverride(sword);
        swordB.Weight = 20;
        var axeB = modB.Weapons.GetOrAddAsOverride(axe);
        axeB.Name = "Renamed Axe";
        await Write(modB, _root.Combine("mods", "B", "B.esp"), new KeyedMasterStyle(A, MasterStyle.Full));

        var translation = new Fallout4Mod(A, Fallout4Release.Fallout4);
        foreach (var weapon in modA.Weapons)
        {
            var copy = weapon.DeepCopy();
            copy.Name = weapon.EditorID switch
            {
                "Sword" => "Eisenschwert",
                "Axe" => "Eisenaxt",
                _ => "Club"
            };
            translation.Weapons.Add(copy);
        }

        var translationFile = _root.Combine("translations", "A.esp");
        await Write(translation, translationFile);

        await WriteProfile(["B", "A"], ["A.esp", "B.esp"]);

        var instance = await Mo2Instance.Load(_root, Game.Fallout4, _root.Combine("game"));
        var analysis = LoadOrderAnalysis.Build(instance, NullLogger.Instance, CancellationToken.None);
        var output = _root.Combine("mods", "Patch", "Patch.esp");
        var result = new TranslationMerger(analysis, German, false, NullLogger.Instance)
            .Merge([new TranslationCandidate("A.esp", translationFile, "test")], output, null, CancellationToken.None);

        var report = Assert.Single(result.Plugins);
        Assert.True(report.Accepted);
        Assert.Equal(1, report.AppliedFields);
        Assert.Equal(1, report.OverriddenLater);

        Assert.Equal([output], result.Outputs);
        var patch = Fallout4Mod.CreateFromBinaryOverlay(new ModPath(output.ToString()), Fallout4Release.Fallout4);
        Assert.True(patch.ModHeader.Flags.HasFlag(Fallout4ModHeader.HeaderFlag.Small));
        var patched = Assert.Single(patch.Weapons);
        Assert.Equal(sword.FormKey, patched.FormKey);
        Assert.Equal("Eisenschwert", patched.Name?.String);
        Assert.Equal(20f, patched.Weight);
    }

    [Fact]
    public async Task RejectsAFileThatOnlySharesTheName()
    {
        var modA = new Fallout4Mod(A, Fallout4Release.Fallout4);
        foreach (var name in new[] {"One", "Two", "Three", "Four"})
        {
            var weapon = modA.Weapons.AddNew(name);
            weapon.Name = name + " Blade";
        }

        await Write(modA, _root.Combine("mods", "A", "A.esp"));

        var impostor = new Fallout4Mod(A, Fallout4Release.Fallout4);
        foreach (var weapon in modA.Weapons)
        {
            var copy = weapon.DeepCopy();
            if (weapon.EditorID == "One") copy.Name = "Klinge Eins";
            impostor.Weapons.Add(copy);
        }

        var file = _root.Combine("translations", "A.esp");
        await Write(impostor, file);
        await WriteProfile(["A"], ["A.esp"]);

        var instance = await Mo2Instance.Load(_root, Game.Fallout4, _root.Combine("game"));
        var analysis = LoadOrderAnalysis.Build(instance, NullLogger.Instance, CancellationToken.None);
        var output = _root.Combine("mods", "Patch", "Patch.esp");
        var result = new TranslationMerger(analysis, German, false, NullLogger.Instance)
            .Merge([new TranslationCandidate("A.esp", file, "test")], output, null, CancellationToken.None);

        Assert.False(Assert.Single(result.Plugins).Accepted);
        Assert.Empty(result.Outputs);
        Assert.False(output.FileExists());
    }

    [Fact]
    public async Task SplitsThePatchWhenItNeedsTooManyMasters()
    {
        const int count = 300;
        var names = Enumerable.Range(0, count).Select(i => $"P{i:000}").ToArray();
        var candidates = new List<TranslationCandidate>();
        var weapons = new List<FormKey>();
        foreach (var name in names)
        {
            var key = ModKey.FromFileName(name + ".esp");
            var mod = new Fallout4Mod(key, Fallout4Release.Fallout4);
            var weapon = mod.Weapons.AddNew(name + "Weapon");
            weapon.Name = name + " Sword";
            weapons.Add(weapon.FormKey);
            await Write(mod, _root.Combine("mods", name, name + ".esp"));

            var translation = new Fallout4Mod(key, Fallout4Release.Fallout4);
            var copy = weapon.DeepCopy();
            copy.Name = name + " Schwert";
            translation.Weapons.Add(copy);
            var file = _root.Combine("translations", name, name + ".esp");
            await Write(translation, file);
            candidates.Add(new TranslationCandidate(name + ".esp", file, "test"));
        }

        await WriteProfile(names.Reverse().ToArray(), names.Select(n => n + ".esp").ToArray());

        var instance = await Mo2Instance.Load(_root, Game.Fallout4, _root.Combine("game"));
        var analysis = LoadOrderAnalysis.Build(instance, NullLogger.Instance, CancellationToken.None);
        var output = _root.Combine("mods", "Patch", "Patch.esp");
        var result = new TranslationMerger(analysis, German, false, NullLogger.Instance)
            .Merge(candidates, output, null, CancellationToken.None);

        Assert.True(result.Outputs.Count >= 2);
        Assert.Equal(output, result.Outputs[0]);
        Assert.Equal(count, result.StringsSet);
        var translated = new HashSet<FormKey>();
        foreach (var part in result.Outputs)
        {
            var mod = Fallout4Mod.CreateFromBinaryOverlay(new ModPath(part.ToString()), Fallout4Release.Fallout4);
            Assert.True(mod.ModHeader.Flags.HasFlag(Fallout4ModHeader.HeaderFlag.Small));
            Assert.True(mod.ModHeader.MasterReferences.Count < 255);
            foreach (var weapon in mod.Weapons)
            {
                Assert.EndsWith(" Schwert", weapon.Name?.String);
                translated.Add(weapon.FormKey);
            }
        }

        Assert.Equal(weapons.ToHashSet(), translated);
    }

    [Fact]
    public async Task WritesALocalizedSkyrimPatchInTheGameLanguage()
    {
        var modA = new SkyrimMod(A, SkyrimRelease.SkyrimSE);
        var sword = modA.Weapons.AddNew("Sword");
        sword.Name = "Iron Sword";
        sword.Description = "A plain blade";
        sword.BasicStats = new WeaponBasicStats {Weight = 10};
        var dagger = modA.Weapons.AddNew("Dagger");
        dagger.Name = "Iron Dagger";
        await WriteSkyrim(modA, _root.Combine("mods", "A", "A.esp"));

        var modB = new SkyrimMod(B, SkyrimRelease.SkyrimSE);
        var swordB = modB.Weapons.GetOrAddAsOverride(sword);
        swordB.BasicStats = new WeaponBasicStats {Weight = 20};
        await WriteSkyrim(modB, _root.Combine("mods", "B", "B.esp"), new KeyedMasterStyle(A, MasterStyle.Full));

        var translation = new SkyrimMod(A, SkyrimRelease.SkyrimSE);
        var copy = sword.DeepCopy();
        copy.Name = "Eisenschwert";
        copy.Description = "A plain blade";
        translation.Weapons.Add(copy);
        var daggerCopy = dagger.DeepCopy();
        daggerCopy.Name = "Eisendolch";
        translation.Weapons.Add(daggerCopy);
        var translationFile = _root.Combine("translations", "A.esp");
        await WriteSkyrim(translation, translationFile);

        await WriteProfile(["B", "A"], ["A.esp", "B.esp"]);
        var german = TranslationGames.SkyrimSpecialEdition.Find("german")!;
        var instance = await Mo2Instance.Load(_root, Game.SkyrimSpecialEdition, _root.Combine("game"));
        var analysis = LoadOrderAnalysis.Build(instance, NullLogger.Instance, CancellationToken.None);
        var output = _root.Combine("mods", "Patch", "Patch.esp");
        var result = new TranslationMerger(analysis, german, true, NullLogger.Instance)
            .Merge([new TranslationCandidate("A.esp", translationFile, "test")], output, null, CancellationToken.None);

        Assert.True(Assert.Single(result.Plugins).Accepted);
        Assert.True(output.Parent.Combine("Strings", "Patch_german.STRINGS").FileExists());
        var patch = SkyrimMod.Create(SkyrimRelease.SkyrimSE).FromPath(new ModPath(output.ToString()))
            .WithKnownMasters(new KeyedMasterStyle(A, MasterStyle.Full), new KeyedMasterStyle(B, MasterStyle.Full))
            .WithDataFolder(output.Parent.ToString()).WithTargetLanguage(Language.German).Construct();
        Assert.True(patch.UsingLocalization);
        Assert.True(patch.ModHeader.Flags.HasFlag(SkyrimModHeader.HeaderFlag.Small));
        var patched = patch.Weapons.Single(w => w.FormKey == sword.FormKey);
        Assert.Equal("Eisendolch", patch.Weapons.Single(w => w.FormKey == dagger.FormKey).Name?.String);
        Assert.Equal("Eisenschwert", patched.Name?.String);
        Assert.Equal("A plain blade", patched.Description?.String);
        Assert.Equal(20f, patched.BasicStats?.Weight);
    }

    [Fact]
    public async Task ListsOnlyFoldersThatAreProfiles()
    {
        await WriteProfile(["A"], ["A.esp"]);
        _root.Combine("profiles", "Leftover", "saves").CreateDirectory();
        Assert.Equal(["Test"], Mo2Instance.ProfileNames(_root));
    }

    [Theory]
    [InlineData("Wabbajack Translation (French)", true)]
    [InlineData("Wabbajack Translation (French, Magnum Opus - Livelier Perks)", true)]
    [InlineData("Wabbajack Voices (German)", true)]
    [InlineData("Wabbajack Language Support (Simplified Chinese)", true)]
    [InlineData("Better Locational Damage", false)]
    public void RecognisesItsOwnMods(string mod, bool expected) =>
        Assert.Equal(expected, TranslationRunner.IsOwnMod(mod));

    [Theory]
    [InlineData("WabbajackTranslation_de.esp", true)]
    [InlineData("*WabbajackTranslation_de_2.esp", true)]
    [InlineData("WabbajackTranslation_fr_12.esp", true)]
    [InlineData("WabbajackTranslation_de_extra.esp", true)]
    [InlineData("SomethingElse.esp", false)]
    public void RecognisesItsOwnPatchParts(string plugin, bool expected) =>
        Assert.Equal(expected, TranslationRunner.IsPatchPlugin(plugin));

    private static async Task WriteSkyrim(SkyrimMod mod, AbsolutePath path, params KeyedMasterStyle[] masters)
    {
        path.Parent.CreateDirectory();
        var loadOrder = masters.Append(new KeyedMasterStyle(mod.ModKey, MasterStyle.Full)).ToArray();
        await mod.BeginWrite.ToPath(path.ToString()).WithLoadOrder(loadOrder).WithUtf8Encoding().WriteAsync();
    }

    private static async Task Write(Fallout4Mod mod, AbsolutePath path, params KeyedMasterStyle[] masters)
    {
        path.Parent.CreateDirectory();
        var loadOrder = masters.Append(new KeyedMasterStyle(mod.ModKey, MasterStyle.Full)).ToArray();
        await mod.BeginWrite.ToPath(path.ToString()).WithLoadOrder(loadOrder).WithUtf8Encoding().WriteAsync();
    }

    private async Task WriteProfile(string[] modsByPriority, string[] loadOrder)
    {
        _root.Combine("game", "Data").CreateDirectory();
        var profile = _root.Combine("profiles", "Test");
        profile.CreateDirectory();
        await _root.Combine("ModOrganizer.ini").WriteAllTextAsync("[General]\r\nselected_profile=@ByteArray(Test)\r\n");
        await profile.Combine("modlist.txt").WriteAllTextAsync(
            "# header\r\n" + string.Join("\r\n", modsByPriority.Select(m => "+" + m)) + "\r\n");
        await profile.Combine("plugins.txt").WriteAllTextAsync(
            "# header\r\n" + string.Join("\r\n", loadOrder.Select(p => "*" + p)) + "\r\n");
        await profile.Combine("loadorder.txt").WriteAllTextAsync(
            "# header\r\n" + string.Join("\r\n", loadOrder) + "\r\n");
    }
}
