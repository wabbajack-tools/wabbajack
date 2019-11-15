using Microsoft.WindowsAPICodePack.Dialogs;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Wabbajack.Lib;

namespace Wabbajack
{
    public class ModlistSettingsEditorVM : ViewModel
    {
        private CompilationModlistSettings settings;
        private string mo2Profile;

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

        public ModlistSettingsEditorVM(CompilationModlistSettings settings, string mo2Profile)
        {
            this.settings = settings;
            this.mo2Profile = mo2Profile;
            this.ImagePath = new FilePickerVM()
            {
                DoExistsCheck = false,
                PathType = FilePickerVM.PathTypeOptions.File,
                Filters =
                {
                    new CommonFileDialogFilter("Banner image", "*.png")
                }
            };
            this.ReadMeText = new FilePickerVM()
            {
                PathType = FilePickerVM.PathTypeOptions.File,
                DoExistsCheck = true,
            };
        }

        public void Init()
        {
            this.AuthorText = settings.Author;
            if (string.IsNullOrWhiteSpace(settings.ModListName))
            {
                // Set ModlistName initially off just the MO2Profile
                this.ModListName = mo2Profile;
            }
            else
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
