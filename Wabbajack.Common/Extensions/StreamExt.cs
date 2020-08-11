using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Wabbajack.Common
{
    public static class StreamExt
    {
        public static long Remaining(this Stream stream)
        {
            return stream.Length - stream.Position;
        }
    }
}
