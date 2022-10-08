using Microsoft.WindowsAPICodePack.Dialogs;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using Wabbajack.Common;
using Wabbajack.Compiler;
using Wabbajack.DTOs;
using Wabbajack.DTOs.GitHub;
using Wabbajack;
using Wabbajack.Extensions;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Consts = Wabbajack.Consts;

namespace Wabbajack
{
    public class MO2CompilerVM : ViewModel
    {
        public CompilerVM Parent { get; }

        private readonly MO2CompilationSettings _settings;

        private readonly ObservableAsPropertyHelper<AbsolutePath> _mo2Folder;
        public AbsolutePath Mo2Folder => _mo2Folder.Value;

        private readonly ObservableAsPropertyHelper<string> _moProfile;
        public string MOProfile => _moProfile.Value;

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

        public MO2CompilerVM(CompilerVM parent)
        {
        }
    }
}
