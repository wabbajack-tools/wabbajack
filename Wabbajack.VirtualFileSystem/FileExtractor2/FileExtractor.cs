using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Compression.BSA;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Readers;
using Wabbajack.Common;
using Wabbajack.Common.FileSignatures;
using Wabbajack.VirtualFileSystem.SevenZipExtractor;

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
                    return await GatheringExtractWith7Zip<T>(archive, (Definitions.FileType)sig, shouldExtract, mapfn);
                
                case Definitions.FileType.TES3:
                case Definitions.FileType.BSA:
                case Definitions.FileType.BA2:
                    return await GatheringExtractWithBSA(sFn, (Definitions.FileType)sig, shouldExtract, mapfn);
                

                default:
                    throw new Exception($"Invalid file format {sFn.Name}");
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
            /*
            IReader reader;
            if (sig == Definitions.FileType._7Z)
                reader = SevenZipArchive.Open(stream).ExtractAllEntries();
            else
            {
                reader = ReaderFactory.Open(stream);
            }

            var results = new Dictionary<RelativePath, T>();
            while (reader.MoveToNextEntry())
            {
                var path = (RelativePath)reader.Entry.Key;
                if (!reader.Entry.IsDirectory && shouldExtract(path))
                {
                    var ms = new MemoryStream();
                    reader.WriteEntryTo(ms);
                    ms.Position = 0;
                    var result = await mapfn(path, new MemoryStreamFactory(ms));
                    results.Add(path, result);
                }
            }

            return results;
            */
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
