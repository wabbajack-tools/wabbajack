using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Compression.BSA;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using OMODFramework;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Readers;
using Wabbajack.Common;
using Wabbajack.Common.FileSignatures;
using Wabbajack.VirtualFileSystem.SevenZipExtractor;
using Utils = Wabbajack.Common.Utils;

namespace Wabbajack.VirtualFileSystem
{
    public static class FileExtractor2
    {
        public static readonly SignatureChecker ArchiveSigs = new SignatureChecker(Definitions.FileType.TES3, 
            Definitions.FileType.BSA,
            Definitions.FileType.BA2,
            Definitions.FileType.ZIP,
            //Definitions.FileType.EXE,
            Definitions.FileType.RAR_OLD,
            Definitions.FileType.RAR_NEW,
            Definitions.FileType._7Z);
        
        private static Extension OMODExtension = new Extension(".omod");
        
        /// <summary>
        /// When true, will allow 7z to use multiple threads and cache more data in memory, potentially
        /// using many GB of RAM during extraction but vastly reducing extraction times in the process.
        /// </summary>
        public static bool FavorPerfOverRAM { get; set; }


        public static async Task<Dictionary<RelativePath, T>> GatheringExtract<T>(IStreamFactory sFn,
            Predicate<RelativePath> shouldExtract, Func<RelativePath, IStreamFactory, ValueTask<T>> mapfn)
        {
            if (sFn is NativeFileStreamFactory)
            {
                Utils.Log($"Extracting {sFn.Name}");
            }
            await using var archive = await sFn.GetStream();
            var sig = await ArchiveSigs.MatchesAsync(archive);
            archive.Position = 0;

            switch (sig)
            {
                case Definitions.FileType.RAR_OLD:
                case Definitions.FileType.RAR_NEW:
                case Definitions.FileType._7Z:
                case Definitions.FileType.ZIP:
                {
                    if (sFn.Name.FileName.Extension == OMODExtension)
                    {
                        return await GatheringExtractWithOMOD(archive, shouldExtract, mapfn);

                    }
                    else
                    {
                        return await GatheringExtractWith7Zip<T>(archive, (Definitions.FileType)sig, shouldExtract,
                            mapfn);
                    }
                }

                case Definitions.FileType.TES3:
                case Definitions.FileType.BSA:
                case Definitions.FileType.BA2:
                    return await GatheringExtractWithBSA(sFn, (Definitions.FileType)sig, shouldExtract, mapfn);
                

                default:
                    throw new Exception($"Invalid file format {sFn.Name}");
            }
        }

        private static async Task<Dictionary<RelativePath,T>> GatheringExtractWithOMOD<T>(Stream archive, Predicate<RelativePath> shouldExtract, Func<RelativePath,IStreamFactory,ValueTask<T>> mapfn)
        {
            var tmpFile = new TempFile();
            await tmpFile.Path.WriteAllAsync(archive);
            var dest = await TempFolder.Create();
            Utils.Log($"Extracting {(string)tmpFile.Path}");

            Framework.Settings.TempPath = (string)dest.Dir;
            Framework.Settings.CodeProgress = new OMODProgress();

            var omod = new OMOD((string)tmpFile.Path);
            omod.GetDataFiles();
            omod.GetPlugins();
            
            var results = new Dictionary<RelativePath, T>();
            foreach (var file in dest.Dir.EnumerateFiles())
            {
                var path = file.RelativeTo(dest.Dir);
                if (!shouldExtract(path)) continue;

                var result = await mapfn(path, new NativeFileStreamFactory(file, path));
                results.Add(path, result);
            }

            return results;
        }
        
        private class OMODProgress : ICodeProgress
        {
            private long _total;

            public void SetProgress(long inSize, long outSize)
            {
                Utils.Status("Extracting OMOD", Percent.FactoryPutInRange(inSize, _total));
            }

            public void Init(long totalSize, bool compressing)
            {
                _total = totalSize;
            }

            public void Dispose()
            {
                //
            }
        }


        private static async Task<Dictionary<RelativePath,T>> GatheringExtractWithBSA<T>(IStreamFactory sFn, Definitions.FileType sig, Predicate<RelativePath> shouldExtract, Func<RelativePath,IStreamFactory,ValueTask<T>> mapfn)
        {
            var archive = await BSADispatch.OpenRead(sFn, sig);
            var results = new Dictionary<RelativePath, T>();
            foreach (var entry in archive.Files)
            {
                if (!shouldExtract(entry.Path))
                    continue;

                var result = await mapfn(entry.Path, await entry.GetStreamFactory());
                results.Add(entry.Path, result);
            }

            return results;
        }

        private static async Task<Dictionary<RelativePath,T>> GatheringExtractWith7Zip<T>(Stream stream, Definitions.FileType sig, Predicate<RelativePath> shouldExtract, Func<RelativePath,IStreamFactory,ValueTask<T>> mapfn)
        {
            return await new GatheringExtractor<T>(stream, sig, shouldExtract, mapfn).Extract();
        }

        public static async Task ExtractAll(AbsolutePath src, AbsolutePath dest)
        {
            await GatheringExtract(new NativeFileStreamFactory(src), _ => true, async (path, factory) =>
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
