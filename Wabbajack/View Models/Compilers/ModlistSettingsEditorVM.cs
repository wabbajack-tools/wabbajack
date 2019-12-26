using System;
using System.Reactive.Linq;
using System.Windows.Input;
using DynamicData;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.Lib;

namespace Wabbajack
{
    public class ModlistSettingsEditorVM : ViewModel
    {
        private CompilationModlistSettings _settings;

        [Reactive]
        public string ModListName { get; set; }

        [Reactive]
        public string AuthorText { get; set; }

        [Reactive]
        public string Description { get; set; }

        public FilePickerVM ImagePath { get; }

        public FilePickerVM ReadmeFilePath { get; }

        [Reactive]
        public string ReadmeWebsite { get; set; }

        [Reactive]
        public string Website { get; set; }

        [Reactive]
        public bool ReadmeIsWebsite { get; set; }

        public IObservable<bool> InError { get; }

        public ICommand SwapToTextReadmeCommand { get; }
        public ICommand SwapToWebsiteReadmeCommand { get; }

        public ModlistSettingsEditorVM(CompilationModlistSettings settings)
        {
            this._settings = settings;
            ImagePath = new FilePickerVM()
            {
                ExistCheckOption = FilePickerVM.CheckOptions.IfPathNotEmpty,
                PathType = FilePickerVM.PathTypeOptions.File,
            };
            ImagePath.Filters.Add(new FilePickerVM.CommonFileDialogFilter("Banner image", "*.png"));
            ReadmeFilePath = new FilePickerVM()
            {
                PathType = FilePickerVM.PathTypeOptions.File,
                ExistCheckOption = FilePickerVM.CheckOptions.IfPathNotEmpty,
            };
            ReadmeFilePath.Filters.Add(new FilePickerVM.CommonFileDialogFilter("Text", "*.txt"));
            ReadmeFilePath.Filters.Add(new FilePickerVM.CommonFileDialogFilter("HTML File", "*.html"));

            InError = Observable.CombineLatest(
                    this.WhenAny(x => x.ImagePath.ErrorState).Select(err => err.Failed),
                    this.WhenAny(x => x.ReadmeFilePath.ErrorState).Select(err => err.Failed),
                    this.WhenAny(x => x.ReadmeIsWebsite),
                resultSelector: (img, readme, isWebsite) => img || (readme && !isWebsite))
                .Publish()
                .RefCount();

            SwapToTextReadmeCommand = ReactiveCommand.Create(() => ReadmeIsWebsite = false);
            SwapToWebsiteReadmeCommand = ReactiveCommand.Create(() => ReadmeIsWebsite = true);
        }

        public void Init()
        {
            AuthorText = _settings.Author;
            if (!string.IsNullOrWhiteSpace(_settings.ModListName))
            {
                ModListName = _settings.ModListName;
            }
            Description = _settings.Description;
            ReadmeIsWebsite = _settings.ReadmeIsWebsite;
            if (ReadmeIsWebsite)
            {
                ReadmeWebsite = _settings.Readme;
            }
            else
            {
                ReadmeFilePath.TargetPath = _settings.Readme;
            }
            ImagePath.TargetPath = _settings.SplashScreen;
            Website = _settings.Website;
        }

        public void Save()
        {
            _settings.Author = AuthorText;
            _settings.ModListName = ModListName;
            _settings.Description = Description;
            _settings.ReadmeIsWebsite = ReadmeIsWebsite;
            if (ReadmeIsWebsite)
            {
                _settings.Readme = ReadmeWebsite;
            }
            else
            {
                _settings.Readme = ReadmeFilePath.TargetPath;
            }
            _settings.SplashScreen = ImagePath.TargetPath;
            _settings.Website = Website;
        }
    }
}
