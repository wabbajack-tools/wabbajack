using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using DeviceId;
using Wabbajack.Common;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Services.OSIntegrated;

public static class ProtectedData
{
    private static readonly Task<byte[]> _deviceKey;

    static ProtectedData()
    {
        
        _deviceKey = Task.Run(async () =>
        {
            var id = Encoding.UTF8.GetBytes(KnownFolders.AppDataLocal.ToString());
            
            var hash1 = await id.Hash();
            var hash2 = new Hash((ulong) hash1 ^ 42);
            var hash3 = new Hash((ulong) hash1 ^ (ulong.MaxValue - 42));

            var hashes = new[] {hash1.ToArray(), hash2.ToArray(), hash3.ToArray()}.ConcatArrays();

            return hashes;
        });
    }

    public static async ValueTask<Stream> Protect(this Stream outStream, string key)
    {
        var encryptor = TripleDES.Create()
            .CreateEncryptor(await _deviceKey, (await Encoding.UTF8.GetBytes(key).Hash()).ToArray());
        return new CryptoStream(outStream, encryptor, CryptoStreamMode.Write);
    }

    public static async ValueTask<Stream> UnProtect(this Stream inStream, string key)
    {
        var encryptor = TripleDES.Create()
            .CreateDecryptor(await _deviceKey, (await Encoding.UTF8.GetBytes(key).Hash()).ToArray());
        return new CryptoStream(inStream, encryptor, CryptoStreamMode.Read);
    }

    public static async Task AsEncryptedJsonFile<T>(this T obj, AbsolutePath destination)
    {
        destination.Parent.CreateDirectory();
        await using var fs = destination.Open(FileMode.Create, FileAccess.Write, FileShare.None);
        await using var enc = await fs.Protect(destination.FileName.ToString());
        await JsonSerializer.SerializeAsync(enc, obj);
        await enc.FlushAsync();
    }

    public static async Task AsEncryptedDataFile(this byte[] obj, AbsolutePath destination)
    {
        destination.Parent.CreateDirectory();
        await using var fs = destination.Open(FileMode.Create, FileAccess.Write, FileShare.None);
        await using var enc = await fs.Protect(destination.FileName.ToString());
        await enc.WriteAsync(obj);
    }

    public static async Task<T?> FromEncryptedJsonFile<T>(this AbsolutePath destination)
    {
        if (!destination.FileExists()) return default;

        await using var fs = destination.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
        await using var enc = await fs.UnProtect(destination.FileName.ToString());
        return await JsonSerializer.DeserializeAsync<T>(enc);
    }

    public static async Task<byte[]> FromEncryptedDataFile(this AbsolutePath destination)
    {
        if (!destination.FileExists()) return Array.Empty<byte>();

        await using var fs = destination.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
        await using var enc = await fs.UnProtect(destination.FileName.ToString());
        return await enc.ReadAllAsync();
    }
}