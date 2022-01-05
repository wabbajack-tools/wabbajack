using System;
using System.Threading.Tasks;
using Wabbajack.Compiler;
using Wabbajack.DTOs;

namespace Wabbajack
{
    public interface ISubCompilerVM
    {
        ACompiler ActiveCompilation { get; }
        ModlistSettingsEditorVM ModlistSettings { get; }
        void Unload();
        IObservable<bool> CanCompile { get; }
        Task<GetResponse<ModList>> Compile();
    }
}
