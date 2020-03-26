using System;

namespace Wabbajack.Common.Test
{
    public static class TestUtils
    {
        private static Random _random = new Random();
        
        public static byte[] RandomData(int? size = null, int maxSize = 1024)
        {
            if (size == null)
                size = _random.Next(1, maxSize);
            var arr = new byte[(int) size];
            _random.NextBytes(arr);
            return arr;
        }
        
        public static object RandomOne(params object[] opts)
        {
            return opts[_random.Next(0, opts.Length)];
        }
        
    }
}
