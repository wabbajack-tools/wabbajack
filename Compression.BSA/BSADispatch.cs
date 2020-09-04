using System.Collections.Generic;
using System.IO;
using System.Text;
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
                Definitions.FileType.TES3 => await TES3Reader.Load(filename),
                Definitions.FileType.BSA => await BSAReader.LoadAsync(filename),
                Definitions.FileType.BA2 => await BA2Reader.Load(filename),
                _ => throw new InvalidDataException("Filename is not a .bsa or .ba2")
            };
        }
        
        private static SignatureChecker BSASignatures = new SignatureChecker(Definitions.FileType.BSA, Definitions.FileType.BA2, Definitions.FileType.TES3);
        public static async ValueTask<bool> MightBeBSA(AbsolutePath filename)
        {
            return await BSASignatures.MatchesAsync(filename) != null;
        }
    }
}
