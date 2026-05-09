using System;
using System.IO.Hashing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wabbajack.Hashing.xxHash64;

public static class StringExtensions
{
    public static string ToHex(this byte[] bytes)
    {
        var builder = new StringBuilder(bytes.Length * 2);
        for (var i = 0; i < bytes.Length; i++) builder.Append(bytes[i].ToString("x2"));
        return builder.ToString();
    }

    public static byte[] FromHex(this string hex)
    {
        return Enumerable.Range(0, hex.Length)
            .Where(x => x % 2 == 0)
            .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
            .ToArray();
    }

    public static string ToBase64(this byte[] data)
    {
        return Convert.ToBase64String(data);
    }

    public static byte[] FromBase64(this string data)
    {
        return Convert.FromBase64String(data);
    }

    public static ValueTask<Hash> Hash(this string s)
    {
        return ValueTask.FromResult(new Hash(XxHash64.HashToUInt64(Encoding.UTF8.GetBytes(s))));
    }
}
