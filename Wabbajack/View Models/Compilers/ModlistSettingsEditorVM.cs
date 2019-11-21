using Microsoft.WindowsAPICodePack.Dialogs;
using ReactiveUI.Fody.Helpers;
using Wabbajack.Lib;

namespace Wabbajack
{
    public class ModlistSettingsEditorVM : ViewModel
    {
        private CompilationModlistSettings settings;

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
            this.settings = settings;
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
            };
        }

        public void Init()
        {
            AuthorText = settings.Author;
            if (!string.IsNullOrWhiteSpace(settings.ModListName))
            {
                ModListName = settings.ModListName;
            }
            Description = settings.Description;
            ReadMeText.TargetPath = settings.Readme;
            ImagePath.TargetPath = settings.SplashScreen;
            Website = settings.Website;
        }

        public void Save()
        {
            settings.Author = AuthorText;
            settings.ModListName = ModListName;
            settings.Description = Description;
            settings.Readme = ReadMeText.TargetPath;
            settings.SplashScreen = ImagePath.TargetPath;
            settings.Website = Website;
        }
    }
}
