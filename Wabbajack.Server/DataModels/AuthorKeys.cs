using System.Threading.Tasks;
using Wabbajack.BuildServer;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Server.DataModels;

public class AuthorKeys
{
    private readonly AppSettings _settings;
    private AbsolutePath AuthorKeysPath => _settings.AuthorAPIKeyFile.ToAbsolutePath();

    public AuthorKeys(AppSettings settings)
    {
        _settings = settings;
    }

    public async Task<string?> AuthorForKey(string key)
    {
        await foreach (var line in AuthorKeysPath.ReadAllLinesAsync())
        {
            var parts = line.Split("\t");
            if (parts[0].Trim() == key)
                return parts[1].Trim();
        }
        return null;
    }
}