using System.Collections.Generic;
using System.IO;
using System.Linq;
using Wabbajack.Common;
using Wabbajack.Lib.CompilationSteps;
using Path = Alphaleonis.Win32.Filesystem.Path;
using System.Threading.Tasks;

namespace Wabbajack.Lib
{
    public class zEditIntegration
    {
        public class IncludeZEditPatches : ACompilationStep
        {
            private readonly Dictionary<AbsolutePath, zEditMerge> _mergesIndexed = new Dictionary<AbsolutePath, zEditMerge>();

            private readonly bool _disabled = true;

            private readonly MO2Compiler _mo2Compiler;

            public IncludeZEditPatches(MO2Compiler compiler) : base(compiler)
            {
                _mo2Compiler = compiler;
                var zEditPath = FindzEditPath(compiler);
                var found = zEditPath != default;

                Utils.Log(found ? $"Found zEdit at {zEditPath}" : "zEdit not detected, disabling zEdit routines");

                if (!found)
                {
                    _mergesIndexed = new Dictionary<AbsolutePath, zEditMerge>();
                    return;
                }
                _mo2Compiler = compiler;

                var settingsFiles = zEditPath.Parent.Combine("profiles").EnumerateFiles()
                    .Where(f => f.IsFile)
                    .Where(f => f.FileName == Consts.SettingsJson)
                    .Where(f =>
                    {
                        var settings = f.FromJson<zEditSettings>();

                        if (settings.modManager != "Mod Organizer 2")
                        {
                            Utils.Log($"zEdit settings file {f}: modManager is not Mod Organizer 2 but {settings.modManager}!");
                            return false;
                        }

                        if (settings.managerPath != _mo2Compiler.SourcePath)
                        {
                            Utils.Log($"zEdit settings file {f}: managerPath is not {_mo2Compiler.SourcePath} but {settings.managerPath}!");
                            return false;
                        }

                        if (settings.modsPath != _mo2Compiler.SourcePath.Combine(Consts.MO2ModFolderName))
                        {
                            Utils.Log($"zEdit settings file {f}: modsPath is not {_mo2Compiler.SourcePath}\\{Consts.MO2ModFolderName} but {settings.modsPath}!");
                            return false;
                        }

                        if (settings.mergePath != _mo2Compiler.SourcePath.Combine(Consts.MO2ModFolderName))
                        {
                            Utils.Log($"zEdit settings file {f}: modsPath is not {_mo2Compiler.SourcePath}\\{Consts.MO2ModFolderName} but {settings.modsPath}!");
                            return false;
                        }

                        return true;
                    }).ToList();

                if (!settingsFiles.Any())
                {
                    Utils.Log($"Found not acceptable settings.json file for zEdit!");
                    return;
                }
                
                var profileFolder =
                    settingsFiles.Where(x => x.Parent.Combine("merges.json").IsFile)
                        .Select(x => x == default ? x : x.Parent)
                        .FirstOrDefault();

                if (profileFolder == default)
                {
                    Utils.Log("Found no acceptable profiles folder for zEdit!");
                    return;
                }

                var mergeFile = profileFolder.Combine("merges.json");

                Utils.Log($"Using merge file {mergeFile}");

                var merges = mergeFile.FromJson<List<zEditMerge>>().GroupBy(f => (f.name, f.filename)).ToArray();

                merges.Where(m => m.Count() > 1)
                    .Do(m =>
                    {
                        Utils.Log(
                            $"WARNING, you have two patches named {m.Key.name}\\{m.Key.filename} in your zEdit profiles. We'll pick one at random, this probably isn't what you want.");
                    });

                _mergesIndexed =
                    merges.ToDictionary(
                        m => _mo2Compiler.SourcePath.Combine((string)Consts.MO2ModFolderName, m.Key.name, m.Key.filename),
                        m => m.First());

                _disabled = false;
            }

            public static AbsolutePath FindzEditPath(MO2Compiler compiler)
            {
                var executables = compiler.MO2Ini.customExecutables;
                if (executables.size == null) return default;

                foreach (var idx in Enumerable.Range(1, int.Parse(executables.size)))
                {
                    var path = (string)executables[$"{idx}\\binary"];
                    if (path == null) continue;

                    if (path.EndsWith("zEdit.exe"))
                        return (AbsolutePath)path;
                }

                return default;
            }

            public override async ValueTask<Directive?> Run(RawSourceFile source)
            {
                if (_disabled) return null;
                if (!_mergesIndexed.TryGetValue(source.AbsolutePath, out var merge))
                {
                    if(source.AbsolutePath.Extension != Consts.SeqExtension)
                        return null;

                    var seqFolder = source.AbsolutePath.Parent;

                    if (seqFolder.FileName != (RelativePath)"seq")
                        return null;

                    var mergeFolder = seqFolder.Parent;
                    var mergeName = mergeFolder.FileName;

                    if (!mergeFolder.Combine(mergeName + ".esp").Exists)
                        return null;

                    var inline = source.EvolveTo<InlineFile>();
                    inline.SourceDataID = await _compiler.IncludeFile(await source.AbsolutePath.ReadAllBytesAsync());
                    return inline;
                }
                var result = source.EvolveTo<MergedPatch>();
                result.Sources.SetTo(merge.plugins.Select(f =>
                {
                    var origPath = (AbsolutePath)Path.Combine(f.dataFolder, f.filename);
                    var paths = new[]
                    {
                        origPath,
                        origPath.WithExtension(new Extension(".mohidden")),
                        origPath.Parent.Combine((RelativePath)"optional", origPath.FileName)
                    };

                    var absPath = paths.FirstOrDefault(file => file.IsFile);

                    if (absPath == default)
                        throw new InvalidDataException(
                            $"File {origPath} is required to build {merge.filename} but it doesn't exist searched in: \n" + string.Join("\n", paths));

                    Hash hash;

                    try
                    {
                        hash = _compiler.VFS.Index.ByRootPath[absPath].Hash;
                    }
                    catch (KeyNotFoundException e)
                    {
                        Utils.Error(e, $"Could not find the key {absPath} in the VFS Index dictionary!");
                        throw;
                    }

                    return new SourcePatch
                    {
                        RelativePath = absPath.RelativeTo(_mo2Compiler.SourcePath),
                        Hash = hash
                    };
                }));

                var srcData = (await result.Sources.SelectAsync(async f => await _mo2Compiler.SourcePath.Combine(f.RelativePath).ReadAllBytesAsync()).ToList())
                    .ConcatArrays();

                var dstData = await source.AbsolutePath.ReadAllBytesAsync();

                await using (var ms = new MemoryStream())
                {
                    await Utils.CreatePatchCached(srcData, dstData, ms);
                    result.PatchID = await _compiler.IncludeFile(ms.ToArray());
                }

                return result;

            }
        }

        public class zEditSettings
        {
            public string modManager = string.Empty;
            public AbsolutePath managerPath;
            public AbsolutePath modsPath;
            public AbsolutePath mergePath;
        }

        public class zEditMerge
        {
            public string name = string.Empty;
            public string filename = string.Empty;
            public List<zEditMergePlugin> plugins = new List<zEditMergePlugin>();

        }

        public class zEditMergePlugin
        {
            public string? filename;
            public string? dataFolder;
        }

        public static void VerifyMerges(MO2Compiler compiler)
        {
            var byName = compiler.InstallDirectives.ToDictionary(f => f.To);

            foreach (var directive in compiler.InstallDirectives.OfType<MergedPatch>())
            {
                foreach (var source in directive.Sources)
                {
                    if (!byName.TryGetValue(source.RelativePath, out var result))
                        throw new InvalidDataException(
                            $"{source.RelativePath} is needed for merged patch {directive.To} but is not included in the install.");

                    if (result.Hash != source.Hash)
                        throw new InvalidDataException($"Hashes for {result.To} needed for zEdit merge sources don't match, this shouldn't happen");
                }
            }
        }

        public static async Task GenerateMerges(MO2Installer installer)
        {
            await installer.ModList
                .Directives
                .OfType<MergedPatch>()
                .PMap(installer.Queue, async m =>
                {
                    Utils.LogStatus($"Generating zEdit merge: {m.To}");

                    var srcData = (await m.Sources.SelectAsync(async s => await installer.OutputFolder.Combine(s.RelativePath).ReadAllBytesAsync())
                        .ToList())
                        .ConcatArrays();

                    var patchData = await installer.LoadBytesFromPath(m.PatchID);

                    await using var fs = await installer.OutputFolder.Combine(m.To).Create();
                    Utils.ApplyPatch(new MemoryStream(srcData), () => new MemoryStream(patchData), fs);
                });
        }
    }
}
