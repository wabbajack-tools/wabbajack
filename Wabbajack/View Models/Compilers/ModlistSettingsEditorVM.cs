using System;
using System.Reactive.Linq;
using DynamicData;
using Microsoft.WindowsAPICodePack.Dialogs;
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

        public FilePickerVM ReadMeText { get; }

        [Reactive]
        public string Website { get; set; }

        public IObservable<bool> InError { get; }

        public ModlistSettingsEditorVM(CompilationModlistSettings settings)
        {
            this._settings = settings;
            ImagePath = new FilePickerVM()
            {
                ExistCheckOption = FilePickerVM.CheckOptions.IfPathNotEmpty,
                PathType = FilePickerVM.PathTypeOptions.File,
            };
            ImagePath.Filters.Add(new CommonFileDialogFilter("Banner image", "*.png"));
            ReadMeText = new FilePickerVM()
            {
                PathType = FilePickerVM.PathTypeOptions.File,
                ExistCheckOption = FilePickerVM.CheckOptions.IfPathNotEmpty,
            };
            ReadMeText.Filters.Add(new CommonFileDialogFilter("Text", "*.txt"));

            InError = Observable.CombineLatest(
                    this.WhenAny(x => x.ImagePath.ErrorState).Select(err => err.Failed),
                    this.WhenAny(x => x.ReadMeText.ErrorState).Select(err => err.Failed),
                resultSelector: (img, readme) => img || readme)
                .Publish()
                .RefCount();
        }

        public void Init()
        {
            AuthorText = _settings.Author;
            if (!string.IsNullOrWhiteSpace(_settings.ModListName))
            {
                ModListName = _settings.ModListName;
            }
            Description = _settings.Description;
            ReadMeText.TargetPath = _settings.Readme;
            ImagePath.TargetPath = _settings.SplashScreen;
            Website = _settings.Website;
        }

        public void Save()
        {
            _settings.Author = AuthorText;
            _settings.ModListName = ModListName;
            _settings.Description = Description;
            _settings.Readme = ReadMeText.TargetPath;
            _settings.SplashScreen = ImagePath.TargetPath;
            _settings.Website = Website;
        }
    }
}
