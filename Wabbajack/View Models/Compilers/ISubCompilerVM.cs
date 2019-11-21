using ReactiveUI;
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
