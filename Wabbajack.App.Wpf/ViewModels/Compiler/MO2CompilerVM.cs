using ReactiveUI.SourceGenerators;
using System;
using System.Threading.Tasks;
using Wabbajack.Compiler;
using Wabbajack.DTOs;

namespace Wabbajack;

public partial class MO2CompilerVM : ViewModel
{
    public BaseCompilerVM Parent { get; }

    public FilePickerVM DownloadLocation { get; }

    public FilePickerVM ModListLocation { get; }

    [Reactive]
    public partial ACompiler ActiveCompilation { get; private set; }

    [Reactive]
    public partial object StatusTracker { get; private set; }

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
