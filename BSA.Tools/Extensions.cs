using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace BSA.Tools
{
    public static class Extensions
    {
        public static string ReadFourCC(this BinaryReader stream)
        {
            byte[] buf = new byte[4];
            stream.Read(buf, 0, 4);
            return new string(buf.Select(b => (char)b).ToArray());
        }
    }
}
