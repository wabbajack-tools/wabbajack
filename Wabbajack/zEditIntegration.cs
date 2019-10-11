using Alphaleonis.Win32.Filesystem;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Wabbajack.Common;
using Directory = Alphaleonis.Win32.Filesystem.Directory;
using File = Alphaleonis.Win32.Filesystem.File;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace Wabbajack
{
    public class zEditIntegration
    {
        public static string FindzEditPath(Compiler compiler)
        {
            var executables = compiler.MO2Ini.customExecutables;
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

        public static Func<RawSourceFile, Directive> IncludezEditPatches(Compiler compiler)
        {
            var zEditPath = FindzEditPath(compiler);
            var havezEdit = zEditPath != null;

            Utils.Log(havezEdit ? $"Found zEdit at {zEditPath}" : $"zEdit not detected, disabling zEdit routines");

            if (!havezEdit) return source => { return null; };

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

            var mergesIndexed =
                merges.ToDictionary(
                    m => Path.Combine(compiler.MO2Folder, "mods", m.Key.name, m.Key.filename),
                    m => m.First());



            return source =>
            {
                if (mergesIndexed.TryGetValue(source.AbsolutePath, out var merge))
                {
                    var result = source.EvolveTo<MergedPatch>();
                    result.Sources = merge.plugins.Select(f =>
                    {
                        var abs_path = Path.Combine(f.dataFolder, f.filename);
                        if (!File.Exists(abs_path))
                            throw new InvalidDataException(
                                $"File {abs_path} is required to build {merge.filename} but it doesn't exist");

                        return new SourcePatch
                        {
                            RelativePath = abs_path.RelativeTo(compiler.MO2Folder),
                            Hash = compiler.VFS[abs_path].Hash
                        };
                    }).ToList();

                    var src_data = merge.plugins.Select(f => File.ReadAllBytes(Path.Combine(f.dataFolder, f.filename)))
                        .ConcatArrays();

                    var dst_data = File.ReadAllBytes(source.AbsolutePath);

                    using (var ms = new MemoryStream())
                    {
                        Utils.CreatePatch(src_data, dst_data, ms);
                        result.PatchID = compiler.IncludeFile(ms.ToArray());
                    }

                    return result;

                }
                return null;
            };


        }


        class zEditMerge
        {
            public string name;
            public string filename;
            public List<zEditMergePlugin> plugins;

        }

        class zEditMergePlugin
        {
            public string filename;
            public string dataFolder;
        }

        public static void VerifyMerges(Compiler compiler)
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
    }
}
