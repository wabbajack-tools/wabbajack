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
using System.Threading.Tasks;

namespace Wabbajack.Lib
{
    public class zEditIntegration
    {
        private static MO2Compiler _mo2Compiler;

        public static AbsolutePath FindzEditPath(ACompiler compiler)
        {
            _mo2Compiler = (MO2Compiler) compiler;
            var executables = _mo2Compiler.MO2Ini.customExecutables;
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

        public class IncludeZEditPatches : ACompilationStep
        {
            private Dictionary<AbsolutePath, zEditMerge> _mergesIndexed;

            public IncludeZEditPatches(ACompiler compiler) : base(compiler)
            {
                var zEditPath = FindzEditPath(compiler);
                var havezEdit = zEditPath != null;

                Utils.Log(havezEdit ? $"Found zEdit at {zEditPath}" : $"zEdit not detected, disabling zEdit routines");

                if (!havezEdit)
                {
                    _mergesIndexed = new Dictionary<AbsolutePath, zEditMerge>();
                    return;
                }

                var merges = zEditPath.Combine("profiles").EnumerateFiles()
                    .Where(f => f.FileName == (RelativePath)"merges.json")
                    .SelectMany(f => f.FromJSON<List<zEditMerge>>())
                    .GroupBy(f => (f.name, f.filename));

                merges.Where(m => m.Count() > 1)
                    .Do(m =>
                    {
                        Utils.Log(
                            $"WARNING, you have two patches named {m.Key.name}\\{m.Key.filename} in your zEdit profiles. We'll pick one at random, this probably isn't what you want.");
                    });

                _mergesIndexed =
                    merges.ToDictionary(
                        m => _mo2Compiler.MO2Folder.Combine(Consts.MO2ModFolderName, (RelativePath)m.Key.name, (RelativePath)m.Key.filename),
                        m => m.First());
            }

            public override async ValueTask<Directive> Run(RawSourceFile source)
            {
                if (!_mergesIndexed.TryGetValue(source.AbsolutePath, out var merge)) return null;
                var result = source.EvolveTo<MergedPatch>();
                result.Sources = merge.plugins.Select(f =>
                {
                    var orig_path = Path.Combine(f.dataFolder, f.filename);
                    var paths = new AbsolutePath?[]
                    {
                        (AbsolutePath)orig_path,
                        (AbsolutePath)(orig_path + ".mohidden"),
                        (AbsolutePath)(Path.Combine(Path.GetDirectoryName(orig_path), "optional", Path.GetFileName(orig_path)))
                    };

                    var absPath = paths.FirstOrDefault(p => p.Value.Exists);

                    if (absPath == null)
                        throw new InvalidDataException(
                            $"File {absPath} is required to build {merge.filename} but it doesn't exist searched in: \n" + String.Join("\n", paths));

                    return new SourcePatch
                    {
                        RelativePath = absPath.Value.RelativeTo(_mo2Compiler.MO2Folder),
                        Hash = _compiler.VFS.Index.ByRootPath[absPath.Value].Hash
                    };
                }).ToList();

                var src_data = result.Sources.Select(f => _mo2Compiler.MO2Folder.Combine(f.RelativePath).ReadAllBytes())
                    .ConcatArrays();

                var dst_data = await source.AbsolutePath.ReadAllBytesAsync();

                await using (var ms = new MemoryStream())
                {
                    await Utils.CreatePatch(src_data, dst_data, ms);
                    result.PatchID = await _compiler.IncludeFile(ms.ToArray());
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

        public static async Task GenerateMerges(MO2Installer installer)
        {
            await installer.ModList
                .Directives
                .OfType<MergedPatch>()
                .PMap(installer.Queue, async m =>
                {
                    Utils.LogStatus($"Generating zEdit merge: {m.To}");

                    var srcData = m.Sources.Select(s => installer.OutputFolder.Combine(s.RelativePath).ReadAllBytes())
                        .ConcatArrays();

                    var patchData = await installer.LoadBytesFromPath(m.PatchID);

                    await using var fs = installer.OutputFolder.Combine(m.To).Create();
                    Utils.ApplyPatch(new MemoryStream(srcData), () => new MemoryStream(patchData), fs);
                });
        }
    }
}
