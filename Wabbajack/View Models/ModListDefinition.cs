using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
