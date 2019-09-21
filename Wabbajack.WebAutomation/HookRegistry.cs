using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wabbajack.WebAutomation.SiteHooks;

namespace Wabbajack.WebAutomation
{
    public class HookRegistry
    {
        private static IEnumerable<ISiteHook> _hooks = new List<ISiteHook>() {new MediaFire()};

        public static ISiteHook FindSiteHook()
        {
            return _hooks.First();
        }
    }
}
