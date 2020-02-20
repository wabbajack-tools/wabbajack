using System.IO;
using System.Text;

namespace Compression.BSA
{
    public static class BSADispatch
    {
        public static IBSAReader OpenRead(string filename)
        {
            var fourcc = "";
            using (var file = File.OpenRead(filename))
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
    }
}
