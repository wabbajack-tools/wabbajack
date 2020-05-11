using System.Collections.Generic;
using System.IO;
using System.Text;
using Wabbajack.Common;

namespace Compression.BSA
{
    public static class BSADispatch
    {
        public static IBSAReader OpenRead(AbsolutePath filename)
        {
            var fourcc = "";
            using (var file = filename.OpenRead())
            {
                fourcc = Encoding.ASCII.GetString(new BinaryReader(file).ReadBytes(4));
            }

            if (fourcc == TES3Reader.TES3_MAGIC)
                return new TES3Reader(filename);
            if (fourcc == "BSA\0")
                return new BSAReader(filename);
            if (fourcc == "BTDX")
                return new BA2Reader(filename);
            throw new InvalidDataException("Filename is not a .bsa or .ba2, magic " + fourcc);
        }

        private static HashSet<string> MagicStrings = new HashSet<string> {TES3Reader.TES3_MAGIC, "BSA\0", "BTDX"};
        public static bool MightBeBSA(AbsolutePath filename)
        {
            using var file = filename.OpenRead();
            var fourcc = Encoding.ASCII.GetString(new BinaryReader(file).ReadBytes(4));
            return MagicStrings.Contains(fourcc);
        }
    }
}
