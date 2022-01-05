using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Wabbajack.DTOs.Logins;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Services.OSIntegrated.TokenProviders;

public class WabbajackApiTokenProvider : ITokenProvider<WabbajackApiState>
{
    private AbsolutePath MetricsPath => KnownFolders.WabbajackAppLocal.Combine("encrypted", "metrics-key");
    private AbsolutePath AuthorKeyPath => KnownFolders.WabbajackAppLocal.Combine("author-api-key.txt");

    public async ValueTask<WabbajackApiState?> Get()
    {
        if (!MetricsPath.FileExists())
            await CreateMetricsKey();

        string wjToken;
        try
        {
            wjToken = (await MetricsPath.FromEncryptedJsonFile<string>())!;
        }
        catch (CryptographicException)
        {
            MetricsPath.Delete();
            await CreateMetricsKey();
            wjToken = (await MetricsPath.FromEncryptedJsonFile<string>())!;
        }

        return new WabbajackApiState
        {
            MetricsKey = wjToken,
            AuthorKey = AuthorKeyPath.FileExists() ? (await AuthorKeyPath.ReadAllTextAsync()).Trim() : null
        };
    }

    public ValueTask SetToken(WabbajackApiState val)
    {
        throw new NotImplementedException();
    }

    public ValueTask<bool> Delete()
    {
        throw new NotImplementedException();
    }

    public bool HaveToken()
    {
        return true;
    }

    private async Task CreateMetricsKey()
    {
        var key = MakeRandomKey();
        await key.AsEncryptedJsonFile(MetricsPath);
    }

    public static string MakeRandomKey()
    {
        var random = new Random();
        var bytes = new byte[32];
        random.NextBytes(bytes);
        return bytes.ToHex();
    }
}