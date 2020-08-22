using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Lib.LibCefHelpers;

namespace Wabbajack.Lib.NexusApi
{
    public class HTMLInterface
    {
        public static async Task<PermissionValue> GetUploadPermissions(Game game, long modId)
        {
            var client = new Lib.Http.Client();
            if (Utils.HaveEncryptedJson("nexus-cookies"))
            {
                var cookies = await Utils.FromEncryptedJson<Helpers.Cookie[]>("nexus-cookies");
                client.AddCookies(cookies);
            }

            var response = await client.GetHtmlAsync($"https://nexusmods.com/{game.MetaData().NexusName}/mods/{modId}");

            var hidden = response.DocumentNode
                .Descendants()
                .Any(n => n.Id == $"{modId}-title" && n.InnerText == "Hidden mod");

            if (hidden) return PermissionValue.Hidden;

            var perm = response.DocumentNode
                .Descendants()
                .Where(d => d.HasClass("permissions-title") && d.InnerHtml == "Upload permission")
                .SelectMany(d => d.ParentNode.ParentNode.GetClasses())
                .FirstOrDefault(perm => perm.StartsWith("permission-"));

            var not_found = response.DocumentNode.Descendants()
                .Where(d => d.Id == $"{modId}-title")
                .Select(d => d.InnerText)
                .FirstOrDefault() == "Not found";
            if (not_found) return PermissionValue.NotFound;

            return perm switch
            {
                "permission-no" => PermissionValue.No,
                "permission-maybe" => PermissionValue.Maybe,
                "permission-yes" => PermissionValue.Yes,
                _ => PermissionValue.No
            };
        }

        public enum PermissionValue : int
        {
            No = 0,
            Yes = 1,
            Maybe = 2,
            Hidden = 3,
            NotFound = 4
        }
    }
}
