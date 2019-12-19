using System;
using System.Threading.Tasks;
using ReactiveUI;
using Wabbajack.Common;
using Wabbajack.Lib;

namespace Wabbajack
{
    public interface ISubCompilerVM
    {
        ACompiler ActiveCompilation { get; }
        ModlistSettingsEditorVM ModlistSettings { get; }
        void Unload();
        IObservable<bool> CanCompile { get; }
        Task Compile();
    }
}
