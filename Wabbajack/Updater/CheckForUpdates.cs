using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wabbajack.Common;

namespace Wabbajack.Updater
{
    public class CheckForUpdates
    {
        private ModList _modlist;
        private string _modlistPath;

        public CheckForUpdates(string path)
        {
            _modlistPath = path;
            _modlist = Installer.LoadFromFile(path);
        }

        public bool FindOutdatedMods()
        {
            var installer = new Installer(_modlistPath, _modlist, "");
            Utils.Log($"Checking links for {_modlist.Archives.Count} archives");

            var results = _modlist.Archives.PMap(f =>
            {
                var result = installer.DownloadArchive(f, false);
                if (result) return false;
                Utils.Log($"Unable to resolve link for {f.Name}. If this is hosted on the Nexus the file may have been removed.");
                return true;

            }).ToList();

            return results.Any();
        }

    }
}
