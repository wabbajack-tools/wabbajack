using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text.Json;
using Wabbajack.DTOs.Logins;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Launcher.Models;

public class LegacyNexusApiKey : ITokenProvider<NexusApiState>
{
    private AbsolutePath TokenPath => KnownFolders.WabbajackAppLocal.Combine("nexusapikey");
    public async ValueTask<NexusApiState?> Get()
    {
        var data = await TokenPath.ReadAllBytesAsync();
        var decoded = ProtectedData.Unprotect(data, Encoding.UTF8.GetBytes("nexusapikey"), DataProtectionScope.LocalMachine);
        var apiKey = JsonSerializer.Deserialize<string>(decoded)!;
        return new NexusApiState()
        {
            ApiKey = apiKey
        };
    }

    public ValueTask SetToken(NexusApiState val)
    {
        throw new System.NotImplementedException();
    }

    public ValueTask<bool> Delete()
    {
        throw new System.NotImplementedException();
    }

    public bool HaveToken()
    {
        return TokenPath.FileExists();
    }
}