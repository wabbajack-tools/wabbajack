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

        public ModlistSettingsEditorVM(CompilationModlistSettings settings)
        {
            this._settings = settings;
            ImagePath = new FilePickerVM()
            {
                ExistCheckOption = FilePickerVM.ExistCheckOptions.IfNotEmpty,
                PathType = FilePickerVM.PathTypeOptions.File,
                Filters =
                {
                    new CommonFileDialogFilter("Banner image", "*.png")
                }
            };
            ReadMeText = new FilePickerVM()
            {
                PathType = FilePickerVM.PathTypeOptions.File,
                ExistCheckOption = FilePickerVM.ExistCheckOptions.IfNotEmpty,
                Filters =
                {
                    new CommonFileDialogFilter("Text", "*.txt"),
                }
            };
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
