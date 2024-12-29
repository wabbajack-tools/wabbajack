using ReactiveUI.Fody.Helpers;
using System;
using System.Threading.Tasks;
using Wabbajack.Compiler;
using Wabbajack.DTOs;

namespace Wabbajack;

public class MO2CompilerVM : ViewModel
{
    public BaseCompilerVM Parent { get; }

    public FilePickerVM DownloadLocation { get; }

    public FilePickerVM ModListLocation { get; }

    [Reactive]
    public ACompiler ActiveCompilation { get; private set; }
    
    [Reactive]
    public object StatusTracker { get; private set; }

    public void Unload()
    {
        throw new NotImplementedException();
    }

    public IObservable<bool> CanCompile { get; }
    public Task<GetResponse<ModList>> Compile()
    {
        throw new NotImplementedException();
    }

    public MO2CompilerVM(BaseCompilerVM parent)
    {
    }
}
