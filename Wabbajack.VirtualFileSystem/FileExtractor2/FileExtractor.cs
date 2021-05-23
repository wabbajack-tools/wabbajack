using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Compression.BSA;
using OMODFramework;
using Wabbajack.Common;
using Wabbajack.Common.FileSignatures;
using Wabbajack.Common.StatusFeed;
using Wabbajack.Common.StatusFeed.Errors;
using Wabbajack.VirtualFileSystem.ExtractedFiles;
using Utils = Wabbajack.Common.Utils;

namespace Wabbajack.VirtualFileSystem
{
    public static class FileExtractor2
    {
        public static readonly SignatureChecker ArchiveSigs = new(Definitions.FileType.TES3,
            Definitions.FileType.BSA,
            Definitions.FileType.BA2,
            Definitions.FileType.ZIP,
            //Definitions.FileType.EXE,
            Definitions.FileType.RAR_OLD,
            Definitions.FileType.RAR_NEW,
            Definitions.FileType._7Z);

        private static Extension OMODExtension = new(".omod");
        private static Extension FOMODExtension = new(".fomod");

        private static Extension BSAExtension = new(".bsa");

        public static readonly HashSet<Extension> ExtractableExtensions = new HashSet<Extension>
        {
            new(".bsa"),
            new(".ba2"),
            new(".7z"),
            new(".7zip"),
            new(".rar"),
            new(".zip"),
            OMODExtension,
            FOMODExtension
        };


        /// <summary>
        /// When true, will allow 7z to use multiple threads and cache more data in memory, potentially
        /// using many GB of RAM during extraction but vastly reducing extraction times in the process.
        /// </summary>
        public static bool FavorPerfOverRAM { get; set; }


        public static async Task<Dictionary<RelativePath, T>> GatheringExtract<T>(WorkQueue queue, IStreamFactory sFn,
            Predicate<RelativePath> shouldExtract, Func<RelativePath, IExtractedFile, ValueTask<T>> mapfn,
            AbsolutePath? tempFolder = null,
            HashSet<RelativePath> onlyFiles = null)
        {
            if (tempFolder == null)
                tempFolder = TempFolder.BaseFolder;

            if (sFn is NativeFileStreamFactory)
            {
                Utils.Log($"Extracting {sFn.Name}");
            }
            await using var archive = await sFn.GetStream();
            var sig = await ArchiveSigs.MatchesAsync(archive);
            archive.Position = 0;

            Dictionary<RelativePath, T> results = new Dictionary<RelativePath, T>();

            switch (sig)
            {
                case Definitions.FileType.RAR_OLD:
                case Definitions.FileType.RAR_NEW:
                case Definitions.FileType._7Z:
                case Definitions.FileType.ZIP:
                {
                    if (sFn.Name.FileName.Extension == OMODExtension)
                    {
                        results = await GatheringExtractWithOMOD(archive, shouldExtract, mapfn);

                    }
                    else
                    {
                        results = await GatheringExtractWith7Zip<T>(queue, sFn, (Definitions.FileType)sig, shouldExtract,
                            mapfn, tempFolder.Value, onlyFiles);
                    }

                    break;
                }

                case Definitions.FileType.BSA:
                case Definitions.FileType.BA2:
                    results = await GatheringExtractWithBSA(sFn, (Definitions.FileType)sig, shouldExtract, mapfn);
                    break;

                case Definitions.FileType.TES3:
                    if (sFn.Name.FileName.Extension == BSAExtension)
                        results = await GatheringExtractWithBSA(sFn, (Definitions.FileType)sig, shouldExtract, mapfn);
                    else
                        throw new Exception($"Invalid file format {sFn.Name}");
                    break;
                default:
                    throw new Exception($"Invalid file format {sFn.Name}");
            }

            if (onlyFiles != null && onlyFiles.Count != results.Count)
            {
                throw new Exception(
                    $"Sanity check error extracting {sFn.Name} - {results.Count} results, expected {onlyFiles.Count}");
            }
            return results;
        }

        private static async Task<Dictionary<RelativePath,T>> GatheringExtractWithOMOD<T>(Stream archive, Predicate<RelativePath> shouldExtract, Func<RelativePath,IExtractedFile,ValueTask<T>> mapfn)
        {
            var tmpFile = new TempFile();
            await tmpFile.Path.WriteAllAsync(archive);
            var dest = await TempFolder.Create();
            Utils.Log($"Extracting {(string)tmpFile.Path}");

            using var omod = new OMOD((string) tmpFile.Path);

            var results = new Dictionary<RelativePath, T>();
            
            omod.ExtractFilesParallel((string) dest.Dir, 4);
            if (omod.HasEntryFile(OMODEntryFileType.PluginsCRC))
                omod.ExtractFiles(false, (string) dest.Dir);

            var files = omod.GetDataFiles();
            if (omod.HasEntryFile(OMODEntryFileType.PluginsCRC))
                files.UnionWith(omod.GetPluginFiles());
            
            foreach (var compressedFile in files)
            {
                var abs = compressedFile.Name.RelativeTo(dest.Dir);
                var rel = abs.RelativeTo(dest.Dir); 
                if (!shouldExtract(rel)) continue;

                var result = await mapfn(rel, new ExtractedNativeFile(abs));
                results.Add(rel, result);
            }
            
            return results;
        }

        private static async Task<Dictionary<RelativePath,T>> GatheringExtractWithBSA<T>(IStreamFactory sFn, Definitions.FileType sig, Predicate<RelativePath> shouldExtract, Func<RelativePath,IExtractedFile,ValueTask<T>> mapfn)
        {
            var archive = await BSADispatch.OpenRead(sFn, sig);
            var results = new Dictionary<RelativePath, T>();
            foreach (var entry in archive.Files)
            {
                if (!shouldExtract(entry.Path))
                    continue;

                var result = await mapfn(entry.Path, new ExtractedMemoryFile(await entry.GetStreamFactory()));
                results.Add(entry.Path, result);
            }

            return results;
        }

        private static async Task<Dictionary<RelativePath,T>> GatheringExtractWith7Zip<T>(WorkQueue queue, IStreamFactory sf, Definitions.FileType sig, Predicate<RelativePath> shouldExtract, Func<RelativePath,IExtractedFile,ValueTask<T>> mapfn,
            AbsolutePath tempPath, HashSet<RelativePath> onlyFiles)
        {
            TempFile tmpFile = null;
            var dest = tempPath.Combine(Guid.NewGuid().ToString());
            dest.CreateDirectory();

            TempFile spoolFile = null;
            AbsolutePath source;

            try
            {
                if (sf.Name is AbsolutePath abs)
                {
                    source = abs;
                }
                else
                {
                    spoolFile = new TempFile(tempPath.Combine(Guid.NewGuid().ToString())
                        .WithExtension(sf.Name.FileName.Extension));
                    await using var s = await sf.GetStream();
                    await spoolFile.Path.WriteAllAsync(s);
                    source = spoolFile.Path;
                }

                Utils.Log($"Extracting {(string)source.FileName}", writeToFile: false);
                Utils.Log($"The contents of {(string)source.FileName} are being extracted to {(string)source.FileName} using 7zip.exe", showInLog: false);

                var process = new ProcessHelper {Path = @"Extractors\7z.exe".RelativeTo(AbsolutePath.EntryPoint),};

                if (onlyFiles != null)
                {
                    //It's stupid that we have to do this, but 7zip's file pattern matching isn't very fuzzy
                    IEnumerable<string> AllVariants(string input)
                    {
                        yield return $"\"{input}\"";
                        yield return $"\"\\{input}\"";
                    }

                    tmpFile = new TempFile();
                    await tmpFile.Path.WriteAllLinesAsync(onlyFiles.SelectMany(f => AllVariants((string)f)).ToArray());
                    process.Arguments = new object[]
                    {
                        "x", "-bsp1", "-y", $"-o\"{dest}\"", source, $"@\"{tmpFile.Path}\"", "-mmt=off"
                    };
                }
                else
                {
                    process.Arguments = new object[] {"x", "-bsp1", "-y", $"-o\"{dest}\"", source, "-mmt=off"};
                }


                var result = process.Output.Where(d => d.Type == ProcessHelper.StreamType.Output)
                    .ForEachAsync(p =>
                    {
                        var (_, line) = p;
                        if (line == null)
                            return;

                        if (line.Length <= 4 || line[3] != '%') return;

                        int.TryParse(line.Substring(0, 3), out var percentInt);
                        Utils.Status($"Extracting {(string)source.FileName} - {line.Trim()}",
                            Percent.FactoryPutInRange(percentInt / 100d));
                    });

                var exitCode = await process.Start();


                if (exitCode != 0)
                {
                    Utils.Fatal(new _7zipReturnError(exitCode, source, dest));
                }
                else
                {
                    Utils.Status($"Extracting {source.FileName} - done", Percent.One, alsoLog: true);
                }

                var results = await dest.EnumerateFiles()
                    .PMap(queue, async f =>
                    {
                        var path = f.RelativeTo(dest);
                        if (!shouldExtract(path)) return ((RelativePath, T))default;
                        var file = new ExtractedNativeFile(f);
                        var result = await mapfn(path, file);
                        await f.DeleteAsync();
                        return (path, result);
                    });

                return results.Where(d => d.Item1 != default)
                    .ToDictionary(d => d.Item1, d => d.Item2);
            }
            finally
            {
                await dest.DeleteDirectory();

                if (tmpFile != null)
                {
                    await tmpFile.DisposeAsync();
                }

                if (spoolFile != null)
                {
                    await spoolFile.DisposeAsync();
                }
            }
        }

        public static async Task ExtractAll(WorkQueue queue, AbsolutePath src, AbsolutePath dest)
        {
            await GatheringExtract(queue, new NativeFileStreamFactory(src), _ => true, async (path, factory) =>
            {
                var abs = path.RelativeTo(dest);
                abs.Parent.CreateDirectory();
                await using var stream = await factory.GetStream();
                await abs.WriteAllAsync(stream);
                return 0;
            });
        }
    }
}
