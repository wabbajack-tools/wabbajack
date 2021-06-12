using System.IO;

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
