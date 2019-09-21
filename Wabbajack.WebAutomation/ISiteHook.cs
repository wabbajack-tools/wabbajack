using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using OpenQA.Selenium;

namespace Wabbajack.WebAutomation
{
    public interface ISiteHook
    {
        string SiteHookName { get; }
        string SiteHookDescription { get;  }
        IEnumerable<(string, string)> RequiredUserParameters { get;  }
        IEnumerable<string> RequiredModMetaInfo { get; }
        void Download(IDictionary<string, string> userParams, IDictionary<string, string> modMeta, string destination);
    }
}
