using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.DTOs.Logins;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Services.OSIntegrated.TokenProviders;

public class WabbajackApiTokenProvider : ITokenProvider<WabbajackApiState>
{
    private readonly AbsolutePath _appLocal;

    /// <summary>
    ///     Every call to the Wabbajack API asks for the metrics key, and on a fresh machine the first few ask at
    ///     once. Unguarded, each of them found no key and created one, colliding on the file; static because the
    ///     file is one per machine, whichever instance asks.
    /// </summary>
    private static readonly SemaphoreSlim MetricsLock = new(1, 1);

    private string? _metricsKey;

    public WabbajackApiTokenProvider() : this(KnownFolders.WabbajackAppLocal)
    {
    }

    internal WabbajackApiTokenProvider(AbsolutePath appLocal)
    {
        _appLocal = appLocal;
    }

    private AbsolutePath MetricsPath => _appLocal.Combine("encrypted", "metrics-key");
    private AbsolutePath AuthorKeyPath => _appLocal.Combine("author-api-key.txt");

    public async ValueTask<WabbajackApiState?> Get()
    {
        return new WabbajackApiState
        {
            MetricsKey = await MetricsKey(),
            AuthorKey = AuthorKeyPath.FileExists() ? (await AuthorKeyPath.ReadAllTextAsync()).Trim() : null
        };
    }

    private async Task<string> MetricsKey()
    {
        await MetricsLock.WaitAsync();
        try
        {
            if (_metricsKey != null) return _metricsKey;

            if (!MetricsPath.FileExists())
                await CreateMetricsKey();

            try
            {
                _metricsKey = (await MetricsPath.FromEncryptedJsonFile<string>())!;
            }
            catch (CryptographicException)
            {
                MetricsPath.Delete();
                await CreateMetricsKey();
                _metricsKey = (await MetricsPath.FromEncryptedJsonFile<string>())!;
            }

            return _metricsKey;
        }
        finally
        {
            MetricsLock.Release();
        }
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
