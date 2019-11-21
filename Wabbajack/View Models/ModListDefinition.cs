using Wabbajack.Lib;
using Wabbajack.Lib.ModListRegistry;

namespace Wabbajack.UI
{
    public class ModListDefinition : ViewModel
    {
        private readonly ModlistMetadata _meta;

        public ModListDefinition(ModlistMetadata meta)
        {
            _meta = meta;
        }

    }
}
