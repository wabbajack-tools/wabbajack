using System.Collections.Generic;
using Wabbajack.Common;
using Wabbajack.Lib;

namespace Wabbajack
{
    public class ManifestVM : ViewModel
    { 
        public Manifest Manifest { get; set; }

        public string Name => !string.IsNullOrWhiteSpace(Manifest.Name) ? Manifest.Name : "Wabbajack Modlist";
        public string Author => !string.IsNullOrWhiteSpace(Manifest.Author) ? $"Created by {Manifest.Author}" : "Created by Jyggalag";
        public string Description => !string.IsNullOrWhiteSpace(Manifest.Description) ? Manifest.Description : "";
        public string InstallSize => $"Install Size: {Manifest.InstallSize.ToFileSizeString()}";
        public string DownloadSize => $"Download Size: {Manifest.DownloadSize.ToFileSizeString()}";

        public IEnumerable<Archive> Archives => Manifest.Archives;

        public ManifestVM(Manifest manifest)
        {
            Manifest = manifest;
        }
    }
}
