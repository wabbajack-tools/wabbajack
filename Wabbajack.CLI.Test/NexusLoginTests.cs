using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Wabbajack.CLI.Verbs;
using Wabbajack.DTOs.Logins;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.Services.OSIntegrated;
using Xunit;

namespace Wabbajack.CLI.Test;

public class NexusLoginTests : IDisposable
{
    private readonly string _testKeyName;
    private readonly AbsolutePath _encryptedPath;

    public NexusLoginTests()
    {
        _testKeyName = "test-nexus-" + Guid.NewGuid().ToString("N")[..8];
        _encryptedPath = KnownFolders.WabbajackAppLocal
            .Combine("encrypted")
            .Combine(_testKeyName.ToRelativePath());
    }

    public void Dispose()
    {
        if (_encryptedPath.FileExists()) _encryptedPath.Delete();
    }

    [Fact]
    public async Task NexusLogin_StoresApiKey()
    {
        var provider = new EncryptedJsonTokenProvider<NexusOAuthState>(NullLogger.Instance, null!, _testKeyName);
        var verb = new NexusLogin(NullLogger<NexusLogin>.Instance, provider);

        var result = await verb.Run("test-api-key-123");

        Assert.Equal(0, result);
        Assert.True(provider.HaveToken());
        var stored = await provider.Get();
        Assert.Equal("test-api-key-123", stored!.ApiKey);
        Assert.Null(stored.OAuth);
    }

    [Fact]
    public async Task NexusLogin_OverwritesExistingKey()
    {
        var provider = new EncryptedJsonTokenProvider<NexusOAuthState>(NullLogger.Instance, null!, _testKeyName);
        var verb = new NexusLogin(NullLogger<NexusLogin>.Instance, provider);

        await verb.Run("original-key");
        await verb.Run("updated-key");

        var stored = await provider.Get();
        Assert.Equal("updated-key", stored!.ApiKey);
    }
}
