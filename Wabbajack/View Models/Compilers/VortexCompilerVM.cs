using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;
using Wabbajack.Lib;

namespace Wabbajack
{
    public class VortexCompilerVM : ViewModel, ISubCompilerVM
    {
        public IReactiveCommand BeginCommand => throw new NotImplementedException();

        public bool Compiling => throw new NotImplementedException();

        public ModlistSettingsEditorVM ModlistSettings => throw new NotImplementedException();

        public void Unload() => throw new NotImplementedException();
    }
}
