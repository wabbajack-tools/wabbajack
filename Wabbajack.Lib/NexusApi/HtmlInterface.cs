using System;
using System.Linq;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Wabbajack.Common;
using Wabbajack.Lib.WebAutomation;

namespace Wabbajack.Lib.NexusApi
{
    public class HTMLInterface
    {
        public static async Task<PermissionValue> GetUploadPermissions(Game game, long modId)
        {
            HtmlDocument response;
            using (var driver = await Driver.Create())
            {
                await driver.NavigateTo(new Uri($"https://nexusmods.com/{game.MetaData().NexusName}/mods/{modId}"));
                TOP:
                response = await driver.GetHtmlAsync();
                
                if (response!.Text!.Contains("This process is automatic. Your browser will redirect to your requested content shortly."))
                {
                    await Task.Delay(5000);
                    goto TOP;
                }
                
            }

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
