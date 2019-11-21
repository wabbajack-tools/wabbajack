using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Wabbajack.Common;
using Wabbajack.Lib;

namespace Wabbajack
{
    public interface ISubCompilerVM
    {
        IReactiveCommand BeginCommand { get; }
        ACompiler ActiveCompilation { get; }

        ModlistSettingsEditorVM ModlistSettings { get; }
        StatusUpdateTracker StatusTracker { get;}
        void Unload();
    }
}
