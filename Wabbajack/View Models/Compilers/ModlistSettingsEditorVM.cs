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
            this.ImagePath = new FilePickerVM()
            {
                ExistCheckOption = FilePickerVM.ExistCheckOptions.IfNotEmpty,
                PathType = FilePickerVM.PathTypeOptions.File,
                Filters =
                {
                    new CommonFileDialogFilter("Banner image", "*.png")
                }
            };
            this.ReadMeText = new FilePickerVM()
            {
                PathType = FilePickerVM.PathTypeOptions.File,
                ExistCheckOption = FilePickerVM.ExistCheckOptions.IfNotEmpty,
            };
        }

        public void Init()
        {
            this.AuthorText = settings.Author;
            if (!string.IsNullOrWhiteSpace(settings.ModListName))
            {
                this.ModListName = settings.ModListName;
            }
            this.Description = settings.Description;
            this.ReadMeText.TargetPath = settings.Readme;
            this.ImagePath.TargetPath = settings.SplashScreen;
            this.Website = settings.Website;
        }

        public void Save()
        {
            settings.Author = this.AuthorText;
            settings.ModListName = this.ModListName;
            settings.Description = this.Description;
            settings.Readme = this.ReadMeText.TargetPath;
            settings.SplashScreen = this.ImagePath.TargetPath;
            settings.Website = this.Website;
        }
    }
}
