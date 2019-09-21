using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenQA.Selenium;
using Wabbajack.Common;

namespace Wabbajack.WebAutomation.SiteHooks
{
    class MediaFire : ISiteHook
    {
        public string SiteHookName => "MediaFire";
        public string SiteHookDescription => "Hook for downloading files from MediaFire";
        public IEnumerable<(string, string)> RequiredUserParameters => new List<(string, string)>();
        public IEnumerable<string> RequiredModMetaInfo => new List<string> {"mediaFireURL"};
        public void Download(IDictionary<string, string> userParams, IDictionary<string, string> modMeta, string dest)
        {
            Utils.Status("Getting webdriver");
            using (var driver = Driver.GetDriver())
            {
                Utils.Status($"Navigating to {modMeta["mediaFireURL"]}");
                driver.Url = modMeta["mediaFireURL"];
                var href = driver.FindElement(By.CssSelector("a.input")).GetAttribute("href");
                var client = driver.ConvertToHTTPClient();
                client.DownloadUrl(href, dest);
            }
            
        }
    }
}
