using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.Common;
using Wabbajack.Lib;

namespace Wabbajack
{
    public class MO2CompilerConfigVM : ViewModel
    {
        private CompilerConfigVM _compilerConfig;

        public string ProfileFileFilter => "modlist.txt|txt";

        [Reactive]
        public string ProfileFilePath { get; set; }

        private readonly ObservableAsPropertyHelper<IErrorResponse> _profileFileError;
        public IErrorResponse ProfileFileError => _profileFileError.Value;

        [Reactive]
        public bool EnableBegin { get; set; }

        public IReactiveCommand BeginCommand { get; }

        public MO2CompilerConfigVM(CompilerConfigVM compilerConfig)
        {
            _compilerConfig = compilerConfig;

            EnableBegin = false;

            _profileFileError = this.WhenAny(x => x.ProfileFilePath)
                .Select(Utils.IsDirectoryPathValid)
                .ToProperty(this, nameof(ProfileFileError));

            BeginCommand = ReactiveCommand.Create(() =>
                {
                    _compilerConfig.Compile(ProfileFilePath);
                }, 
                this.WhenAny(x => x.EnableBegin).Select(x => x));

            this.WhenAny(x => x.ProfileFilePath).Subscribe(path =>
            {
                if (string.IsNullOrWhiteSpace(ProfileFilePath)) return;
                EnableBegin = true;
            }).DisposeWith(CompositeDisposable);
        }
    }
}
