using Alphaleonis.Win32.Filesystem;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Wabbajack.Common;
using Wabbajack.Lib.CompilationSteps;
using Directory = Alphaleonis.Win32.Filesystem.Directory;
using File = Alphaleonis.Win32.Filesystem.File;
using Path = Alphaleonis.Win32.Filesystem.Path;
using System.Threading.Tasks;

namespace Wabbajack.Lib
{
    public class zEditIntegration
    {
        private static MO2Compiler _mo2Compiler;

        public static string FindzEditPath(ACompiler compiler)
        {
            _mo2Compiler = (MO2Compiler) compiler;
            var executables = _mo2Compiler.MO2Ini.customExecutables;
            if (executables.size == null) return null;

            foreach (var idx in Enumerable.Range(1, int.Parse(executables.size)))
            {
                var path = (string)executables[$"{idx}\\binary"];
                if (path == null) continue;

                if (path.EndsWith("zEdit.exe"))
                    return Path.GetDirectoryName(path);
            }

            return null;
        }

        public class IncludeZEditPatches : ACompilationStep
        {
            private readonly Dictionary<string, zEditMerge> _mergesIndexed;

            private bool _disabled = true;

            public IncludeZEditPatches(ACompiler compiler) : base(compiler)
            {
                var zEditPath = FindzEditPath(compiler);
                var havezEdit = zEditPath != null;

                Utils.Log(havezEdit ? $"Found zEdit at {zEditPath}" : "zEdit not detected, disabling zEdit routines");

                if (!havezEdit)
                {
                    _mergesIndexed = new Dictionary<string, zEditMerge>();
                    return;
                }
                _mo2Compiler = (MO2Compiler) compiler;
                
                var settingsFiles = Directory.EnumerateFiles(Path.Combine(zEditPath, "profiles"),
                        DirectoryEnumerationOptions.Files | DirectoryEnumerationOptions.Recursive)
                    .Where(f => f.EndsWith("settings.json"))
                    .Where(f =>
                    {
                        var settings = f.FromJSON<zEditSettings>();

                        if (settings.modManager != "Mod Organizer 2")
                        {
                            Utils.Log($"zEdit settings file {f}: modManager is not Mod Organizer 2 but {settings.modManager}!");
                            return false;
                        }

                        if (settings.managerPath != _mo2Compiler.MO2Folder)
                        {
                            Utils.Log($"zEdit settings file {f}: managerPath is not {_mo2Compiler.MO2Folder} but {settings.managerPath}!");
                            return false;
                        }

                        if (settings.modsPath != Path.Combine(_mo2Compiler.MO2Folder, "mods"))
                        {
                            Utils.Log($"zEdit settings file {f}: modsPath is not {_mo2Compiler.MO2Folder}\\mods but {settings.modsPath}!");
                            return false;
                        }

                        if (settings.mergePath != Path.Combine(_mo2Compiler.MO2Folder, "mods"))
                        {
                            Utils.Log($"zEdit settings file {f}: modsPath is not {_mo2Compiler.MO2Folder}\\mods but {settings.modsPath}!");
                            return false;
                        }

                        return true;
                    });

                if (!settingsFiles.Any())
                {
                    Utils.Log($"Found not acceptable settings.json file for zEdit!");
                    return;
                }
                
                var profileFolder =
                    settingsFiles.Where(x => File.Exists(Path.Combine(Path.GetDirectoryName(x), "merges.json")))?.Select(x => string.IsNullOrWhiteSpace(x) ? "" : Path.GetDirectoryName(x)).FirstOrDefault();

                if (string.IsNullOrWhiteSpace(profileFolder))
                {
                    Utils.Log("Found no acceptable profiles folder for zEdit!");
                    return;
                }

                var mergeFile = Path.Combine(profileFolder, "merges.json");

                Utils.Log($"Using merge file {mergeFile}");

                var merges = mergeFile.FromJSON<List<zEditMerge>>().GroupBy(f => (f.name, f.filename));

                merges.Where(m => m.Count() > 1)
                    .Do(m =>
                    {
                        Utils.Log(
                            $"WARNING, you have two patches named {m.Key.name}\\{m.Key.filename} in your zEdit profiles. We'll pick one at random, this probably isn't what you want.");
                    });

                _mergesIndexed =
                    merges.ToDictionary(
                        m => Path.Combine(_mo2Compiler.MO2Folder, Consts.MO2ModFolderName, m.Key.name, m.Key.filename),
                        m => m.First());

                _disabled = false;
            }

            public override async ValueTask<Directive> Run(RawSourceFile source)
            {
                if (_disabled) return null;
                if (!_mergesIndexed.TryGetValue(source.AbsolutePath, out var merge)) return null;
                var result = source.EvolveTo<MergedPatch>();
                result.Sources = merge.plugins.Select(f =>
                {
                    var origPath = Path.Combine(f.dataFolder, f.filename);
                    var paths = new[]
                    {
                        origPath,
                        origPath + ".mohidden",
                        Path.Combine(Path.GetDirectoryName(origPath), "optional", Path.GetFileName(origPath))
                    };

                    var absPath = paths.FirstOrDefault(File.Exists);

                    if (absPath == null)
                        throw new InvalidDataException(
                            $"File {origPath} is required to build {merge.filename} but it doesn't exist searched in: \n" + string.Join("\n", paths));

                    string hash = "";

                    try
                    {
                        hash = _compiler.VFS.Index.ByFullPath[absPath].Hash;
                    } catch (KeyNotFoundException e)
                    {
                        Utils.ErrorThrow(e, $"Could not find the key {absPath} in the VFS Index dictionary!");
                    }

                    return new SourcePatch
                    {
                        RelativePath = absPath.RelativeTo(_mo2Compiler.MO2Folder),
                        Hash = hash
                    };
                }).ToList();

                var src_data = result.Sources.Select(f => File.ReadAllBytes(Path.Combine(_mo2Compiler.MO2Folder, f.RelativePath)))
                    .ConcatArrays();

                var dst_data = File.ReadAllBytes(source.AbsolutePath);

                await using (var ms = new MemoryStream())
                {
                    await Utils.CreatePatch(src_data, dst_data, ms);
                    result.PatchID = _compiler.IncludeFile(ms.ToArray());
                }

                return result;

            }

            public override IState GetState()
            {
                return new State();
            }

            [JsonObject("IncludeZEditPatches")]
            public class State : IState
            {
                public ICompilationStep CreateStep(ACompiler compiler)
                {
                    return new IncludeZEditPatches(compiler);
                }
            }
        }

        public class zEditSettings
        {
            public string modManager;
            public string managerPath;
            public string modsPath;
            public string mergePath;
        }

        public class zEditMerge
        {
            public string name;
            public string filename;
            public List<zEditMergePlugin> plugins;

        }

        public class zEditMergePlugin
        {
            public string filename;
            public string dataFolder;
        }

        public static void VerifyMerges(MO2Compiler compiler)
        {
            var by_name = compiler.InstallDirectives.ToDictionary(f => f.To);

            foreach (var directive in compiler.InstallDirectives.OfType<MergedPatch>())
            {
                foreach (var source in directive.Sources)
                {
                    if (by_name.TryGetValue(source.RelativePath, out var result))
                    {
                        if (result.Hash != source.Hash)
                            throw new InvalidDataException($"Hashes for {result.To} needed for zEdit merge sources don't match, this shouldn't happen");
                        continue;
                    }
                    throw new InvalidDataException($"{source.RelativePath} is needed for merged patch {directive.To} but is not included in the install.");
                }
            }
        }

        public static async Task GenerateMerges(MO2Installer installer)
        {
            await installer.ModList
                .Directives
                .OfType<MergedPatch>()
                .PMap(installer.Queue, m =>
                {
                    Utils.LogStatus($"Generating zEdit merge: {m.To}");

                    var src_data = m.Sources.Select(s => File.ReadAllBytes(Path.Combine(installer.OutputFolder, s.RelativePath)))
                        .ConcatArrays();

                    var patch_data = installer.LoadBytesFromPath(m.PatchID);

                    using (var fs = File.Open(Path.Combine(installer.OutputFolder, m.To), FileMode.Create)) 
                        Utils.ApplyPatch(new MemoryStream(src_data), () => new MemoryStream(patch_data), fs);
                });
        }
    }
}
