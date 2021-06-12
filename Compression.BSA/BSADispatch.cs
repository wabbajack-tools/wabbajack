using System;
using System.IO;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Common.FileSignatures;

namespace Compression.BSA
{
    public static class BSADispatch
    {
        public static async ValueTask<IBSAReader> OpenRead(AbsolutePath filename)
        {
            return await BSASignatures.MatchesAsync(filename) switch
            {
                Definitions.FileType.TES3 => await TES3Reader.Load(new NativeFileStreamFactory(filename)),
                Definitions.FileType.BSA => await BSAReader.LoadAsync(new NativeFileStreamFactory(filename)),
                Definitions.FileType.BA2 => await BA2Reader.Load(new NativeFileStreamFactory(filename)),
                _ => throw new InvalidDataException("Filename is not a .bsa or .ba2")
            };
        }
        
        private static SignatureChecker BSASignatures = new SignatureChecker(Definitions.FileType.BSA, Definitions.FileType.BA2, Definitions.FileType.TES3);
        public static async ValueTask<bool> MightBeBSA(AbsolutePath filename)
        {
            return await BSASignatures.MatchesAsync(filename) != null;
        }

        public static async ValueTask<IBSAReader> OpenRead(IStreamFactory sFn, Definitions.FileType sig)
        {
            switch(sig)
            {
                case Definitions.FileType.TES3:
                    return await TES3Reader.Load(sFn);
                case Definitions.FileType.BSA:
                    return await BSAReader.LoadAsync(sFn);
                case Definitions.FileType.BA2:
                    return await BA2Reader.Load(sFn);
                default:
                    throw new Exception($"Bad archive format for {sFn.Name}");
                
            }
        }
    }
}
