using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Wabbajack
{
    public interface ISubCompilerVM
    {
        IReactiveCommand BeginCommand { get; }
        bool Compiling { get; }
        ModlistSettingsEditorVM ModlistSettings { get; }
    }
}
