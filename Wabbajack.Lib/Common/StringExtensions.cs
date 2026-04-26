using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Wabbajack.Hashing.xxHash64;

namespace Wabbajack.Common;

public static class StringExtensions
{
    public static string StringSha256Hex(this string s)
    {
        var sha = SHA256.Create();
        using (var o = new CryptoStream(Stream.Null, sha, CryptoStreamMode.Write))
        {
            using var i = new MemoryStream(Encoding.UTF8.GetBytes(s));
            i.CopyTo(o);
        }

        return sha.Hash!.ToHex();
    }

    public static bool ContainsCaseInsensitive(this string src, string contains)
    {
        return src.Contains(contains, StringComparison.InvariantCultureIgnoreCase);
    }

    public static string CleanIniString(this string src)
    {
        return src.TrimStart('\"').TrimEnd('\"');
    }
}