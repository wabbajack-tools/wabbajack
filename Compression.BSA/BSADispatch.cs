using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wabbajack.Common.CSP;

namespace Compression.BSA
{
    public static class BSADispatch
    {
        public static Task<IBSAReader> OpenRead(string filename)
        {
            return CSPExtensions.ThreadedTask<IBSAReader>(() =>
            {
                string fourcc = "";
                using (var file = File.OpenRead(filename))
                {
                    fourcc = Encoding.ASCII.GetString(new BinaryReader(file).ReadBytes(4));
                }

                if (fourcc == "BSA\0")
                    return new BSAReader(filename);
                if (fourcc == "BTDX")
                    return new BA2Reader(filename);
                throw new InvalidDataException("Filename is not a .bsa or .ba2, magic " + fourcc);

            });
        }
    }
}
