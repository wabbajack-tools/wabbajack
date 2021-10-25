using System;
using System.Collections.Generic;
using System.Linq;

namespace Wabbajack.Common;

public static class ByteArrayExtensions
{
    public static byte[] ConcatArrays(this IReadOnlyCollection<byte[]> arrays)
    {
        var outArray = new byte[arrays.Sum(a => a.Length)];
        var offset = 0;
        foreach (var arr in arrays)
        {
            Array.Copy(arr, 0, outArray, offset, arr.Length);
            offset += arr.Length;
        }

        return outArray;
    }
}