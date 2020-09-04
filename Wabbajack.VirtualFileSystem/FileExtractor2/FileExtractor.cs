using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
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
            Definitions.FileType.EXE,
            Definitions.FileType.RAR,
            Definitions.FileType._7Z);


        public static async Task<Dictionary<RelativePath, T>> GatheringExtract<T>(IStreamFactory sFn,
            Predicate<RelativePath> shouldExtract, Func<RelativePath, IStreamFactory, ValueTask<T>> mapfn)
        {
            await using var archive = await sFn.GetStream();
            var sig = await ArchiveSigs.MatchesAsync(archive);
            archive.Position = 0;

            switch (sig)
            {
                case Definitions.FileType.ZIP:
                    return await GatheringExtractWith7Zip<T>(archive, (Definitions.FileType)sig, shouldExtract, mapfn);
                
                default:
                    throw new Exception("Invalid file format");
            }
        }

        private static async Task<Dictionary<RelativePath,T>> GatheringExtractWith7Zip<T>(Stream stream, Definitions.FileType sig, Predicate<RelativePath> shouldExtract, Func<RelativePath,IStreamFactory,ValueTask<T>> mapfn)
        {
            return await new GatheringExtractor<T>(stream, sig, shouldExtract, mapfn).Extract();
        }
    }
}
