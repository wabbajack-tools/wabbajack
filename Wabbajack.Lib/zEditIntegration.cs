using Alphaleonis.Win32.Filesystem;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Wabbajack.Common;
using Wabbajack.Lib.CompilationSteps;
using Directory = Alphaleonis.Win32.Filesystem.Directory;
using File = Alphaleonis.Win32.Filesystem.File;
using Path = Alphaleonis.Win32.Filesystem.Path;

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
            private Dictionary<string, zEditMerge> _mergesIndexed;

            public IncludeZEditPatches(ACompiler compiler) : base(compiler)
            {
                var zEditPath = FindzEditPath(compiler);
                var havezEdit = zEditPath != null;

                Utils.Log(havezEdit ? $"Found zEdit at {zEditPath}" : $"zEdit not detected, disabling zEdit routines");

                if (!havezEdit)
                {
                    _mergesIndexed = new Dictionary<string, zEditMerge>();
                    return;
                }

                var merges = Directory.EnumerateFiles(Path.Combine(zEditPath, "profiles"),
                        DirectoryEnumerationOptions.Files | DirectoryEnumerationOptions.Recursive)
                    .Where(f => f.EndsWith("\\merges.json"))
                    .SelectMany(f => f.FromJSON<List<zEditMerge>>())
                    .GroupBy(f => (f.name, f.filename));

                merges.Where(m => m.Count() > 1)
                    .Do(m =>
                    {
                        Utils.Warning(
                            $"WARNING, you have two patches named {m.Key.name}\\{m.Key.filename} in your zEdit profiles. We'll pick one at random, this probably isn't what you want.");
                    });

                _mergesIndexed =
                    merges.ToDictionary(
                        m => Path.Combine(_mo2Compiler.MO2Folder, "mods", m.Key.name, m.Key.filename),
                        m => m.First());
            }

            public override Directive Run(RawSourceFile source)
            {
                if (!_mergesIndexed.TryGetValue(source.AbsolutePath, out var merge)) return null;
                var result = source.EvolveTo<MergedPatch>();
                result.Sources = merge.plugins.Select(f =>
                {
                    var orig_path = Path.Combine(f.dataFolder, f.filename);
                    var paths = new[]
                    {
                        orig_path,
                        orig_path + ".mohidden",
                        Path.Combine(Path.GetDirectoryName(orig_path), "optional", Path.GetFileName(orig_path))
                    };

                    var abs_path = paths.FirstOrDefault(p => File.Exists(p));

                    if (abs_path == null)
                        throw new InvalidDataException(
                            $"File {abs_path} is required to build {merge.filename} but it doesn't exist searched in: \n" + String.Join("\n", paths));

                    return new SourcePatch
                    {
                        RelativePath = abs_path.RelativeTo(_mo2Compiler.MO2Folder),
                        Hash = _compiler.VFS.Index.ByFullPath[abs_path].Hash
                    };
                }).ToList();

                var src_data = result.Sources.Select(f => File.ReadAllBytes(Path.Combine(_mo2Compiler.MO2Folder, f.RelativePath)))
                    .ConcatArrays();

                var dst_data = File.ReadAllBytes(source.AbsolutePath);

                using (var ms = new MemoryStream())
                {
                    Utils.CreatePatch(src_data, dst_data, ms);
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

        public static void GenerateMerges(MO2Installer installer)
        {
            installer.ModList
                .Directives
                .OfType<MergedPatch>()
                .PMap(_mo2Compiler.Queue, m =>
                {
                    Utils.LogStatus($"Generating zEdit merge: {m.To}");

                    var src_data = m.Sources.Select(s => File.ReadAllBytes(Path.Combine(installer.OutputFolder, s.RelativePath)))
                        .ConcatArrays();

                    var patch_data = installer.LoadBytesFromPath(m.PatchID);

                    using (var fs = File.OpenWrite(Path.Combine(installer.OutputFolder, m.To))) 
                        BSDiff.Apply(new MemoryStream(src_data), () => new MemoryStream(patch_data), fs);
                });
        }
    }
}
