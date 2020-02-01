using Wabbajack.Lib;

namespace Wabbajack.View_Models
{
    public class ManifestVM : ViewModel
    {
        public readonly Manifest Manifest;

        public ManifestVM(ModList modlist)
        {
            Manifest = new Manifest(modlist);
        }
    }
}
