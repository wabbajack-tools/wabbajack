namespace Wabbajack.Networking.NexusApi;

public static class Endpoints
{
    public const string Validate = "v1/users/validate.json";
    public const string ModInfo = "v1/games/{0}/mods/{1}.json";
    public const string ModFiles = "v1/games/{0}/mods/{1}/files.json";
    public const string ModFile = "v1/games/{0}/mods/{1}/files/{2}.json";
    public const string DownloadLink = "v1/games/{0}/mods/{1}/files/{2}/download_link.json";
    public const string Updates = "v1/games/{0}/mods/updated.json?period={1}";
    
    public const string OAuthValidate = "https://users.nexusmods.com/oauth/userinfo";
}