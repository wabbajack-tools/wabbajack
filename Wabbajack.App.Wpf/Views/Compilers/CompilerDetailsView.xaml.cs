using System;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using ReactiveUI;
using System.Windows;
using System.Windows.Forms;
using DynamicData;
using Microsoft.WindowsAPICodePack.Dialogs;
using Wabbajack.Common;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.ViewModels.Controls;
using Wabbajack.Services.OSIntegrated;

namespace Wabbajack
{
    /// <summary>
    /// Interaction logic for CompilerDetailsView.xaml
    /// </summary>
    public partial class CompilerDetailsView : ReactiveUserControl<CompilerDetailsVM>
    {
        public CompilerDetailsView()
        {
            InitializeComponent();

            this.WhenActivated(disposables =>
            {
                ViewModel.WhenAny(vm => vm.State)
                    .Select(x => x == CompilerState.Errored)
                    .BindToStrict(this, x => x.CompilationComplete.AttentionBorder.Failure)
                    .DisposeWith(disposables);
                
                ViewModel.WhenAny(vm => vm.State)
                    .Select(x => x == CompilerState.Errored)
                    .Select(failed => $"Compilation {(failed ? "Failed" : "Complete")}")
                    .BindToStrict(this, x => x.CompilationComplete.TitleText.Text)
                    .DisposeWith(disposables);

                ViewModel.WhenAny(vm => vm.ModListImageLocation.TargetPath)
                    .Where(i => i.FileExists())
                    .Select(i => (UIUtils.TryGetBitmapImageFromFile(i, out var img), img))
                    .Where(i => i.Item1)
                    .Select(i => i.img)
                    .BindToStrict(this, view => view.DetailImage.Image);

                ViewModel.WhenAny(vm => vm.Settings.ModListName)
                    .BindToStrict(this, view => view.DetailImage.Title);
                
                ViewModel.WhenAny(vm => vm.Settings.ModListAuthor)
                    .BindToStrict(this, view => view.DetailImage.Author);

                ViewModel.WhenAny(vm => vm.Settings.ModListDescription)
                    .BindToStrict(this, view => view.DetailImage.Description);

                CompilationComplete.GoToModlistButton.Command = ReactiveCommand.Create(() =>
                {
                    UIUtils.OpenFolder(ViewModel.OutputLocation.TargetPath);
                }).DisposeWith(disposables);

                ViewModel.WhenAnyValue(vm => vm.BackCommand)
                    .BindToStrict(this, view => view.CompilationComplete.BackButton.Command)
                    .DisposeWith(disposables);

                CompilationComplete.CloseWhenCompletedButton.Command = ReactiveCommand.Create(() =>
                {
                    Environment.Exit(0);
                }).DisposeWith(disposables);

                
                ViewModel.WhenAnyValue(vm => vm.ExecuteCommand)
                    .BindToStrict(this, view => view.BeginButton.Command)
                    .DisposeWith(disposables);
                
                ViewModel.WhenAnyValue(vm => vm.State)
                    .Select(v => v == CompilerState.Configuration ? Visibility.Visible : Visibility.Collapsed)
                    .BindToStrict(this, view => view.BottomCompilerSettingsGrid.Visibility)
                    .DisposeWith(disposables);

                ViewModel.WhenAnyValue(vm => vm.State)
                    .Select(v => v != CompilerState.Configuration ? Visibility.Visible : Visibility.Collapsed)
                    .BindToStrict(this, view => view.LogView.Visibility)
                    .DisposeWith(disposables);

                ViewModel.WhenAnyValue(vm => vm.State)
                    .Select(v => v == CompilerState.Compiling ? Visibility.Visible : Visibility.Collapsed)
                    .BindToStrict(this, view => view.CpuView.Visibility)
                    .DisposeWith(disposables);
                
                ViewModel.WhenAnyValue(vm => vm.State)
                    .Select(v => v is CompilerState.Completed or CompilerState.Errored ? Visibility.Visible : Visibility.Collapsed)
                    .BindToStrict(this, view => view.CompilationComplete.Visibility)
                    .DisposeWith(disposables);

                ViewModel.WhenAnyValue(vm => vm.ModlistLocation)
                    .BindToStrict(this, view => view.CompilerConfigView.ModListLocation.PickerVM)
                    .DisposeWith(disposables);
                
                ViewModel.WhenAnyValue(vm => vm.DownloadLocation)
                    .BindToStrict(this, view => view.CompilerConfigView.DownloadsLocation.PickerVM)
                    .DisposeWith(disposables);

                ViewModel.WhenAnyValue(vm => vm.Settings.Downloads)
                         .BindToStrict(this, view => view.CompilerConfigView.DownloadsLocation.PickerVM.TargetPath)
                         .DisposeWith(disposables);
                
                ViewModel.WhenAnyValue(vm => vm.OutputLocation)
                    .BindToStrict(this, view => view.CompilerConfigView.OutputLocation.PickerVM)
                    .DisposeWith(disposables);

                ViewModel.WhenAnyValue(vm => vm.Settings.OutputFile)
                         .BindToStrict(this, view => view.CompilerConfigView.OutputLocation.PickerVM.TargetPath)
                         .DisposeWith(disposables);
                
                UserInterventionsControl.Visibility = Visibility.Collapsed;
                
                // Errors
                this.WhenAnyValue(view => view.ViewModel.ErrorState)
                    .Select(x => !x.Failed)
                    .BindToStrict(this, view => view.BeginButton.IsEnabled)
                    .DisposeWith(disposables);
                
                this.WhenAnyValue(view => view.ViewModel.ErrorState)
                    .Select(x => x.Failed ? Visibility.Visible : Visibility.Hidden)
                    .BindToStrict(this, view => view.ErrorSummaryIcon.Visibility)
                    .DisposeWith(disposables);
                
                this.WhenAnyValue(view => view.ViewModel.ErrorState)
                    .Select(x => x.Failed ? Visibility.Visible : Visibility.Hidden)
                    .BindToStrict(this, view => view.ErrorSummaryIconGlow.Visibility)
                    .DisposeWith(disposables);
                
                this.WhenAnyValue(view => view.ViewModel.ErrorState)
                    .Select(x => x.Reason)
                    .BindToStrict(this, view => view.ErrorSummaryIcon.ToolTip)
                    .DisposeWith(disposables);

                
                // Settings 
                
                this.Bind(ViewModel, vm => vm.Settings.ModListName, view => view.ModListNameSetting.Text)
                    .DisposeWith(disposables);
                
                this.Bind(ViewModel, vm => vm.Settings.Profile, view => view.SelectedProfile.Text)
                    .DisposeWith(disposables);
                
                this.Bind(ViewModel, vm => vm.Settings.ModListAuthor, view => view.AuthorNameSetting.Text)
                    .DisposeWith(disposables);
                
                this.Bind(ViewModel, vm => vm.Settings.Version, view => view.VersionSetting.Text)
                    .DisposeWith(disposables);

                this.Bind(ViewModel, vm => vm.Settings.ModListDescription, view => view.DescriptionSetting.Text)
                    .DisposeWith(disposables);

                
                this.Bind(ViewModel, vm => vm.ModListImageLocation, view => view.ImageFilePicker.PickerVM)
                    .DisposeWith(disposables);

                this.Bind(ViewModel, vm => vm.Settings.ModListImage, view => view.ImageFilePicker.PickerVM.TargetPath)
                    .DisposeWith(disposables);
                
                this.Bind(ViewModel, vm => vm.Settings.ModListWebsite, view => view.WebsiteSetting.Text)
                    .DisposeWith(disposables);
                
                this.Bind(ViewModel, vm => vm.Settings.ModListReadme, view => view.ReadmeSetting.Text)
                    .DisposeWith(disposables);
                
                this.Bind(ViewModel, vm => vm.Settings.ModlistIsNSFW, view => view.NSFWSetting.IsChecked)
                    .DisposeWith(disposables);
                
                this.Bind(ViewModel, vm => vm.Settings.PublishUpdate, view => view.PublishUpdate.IsChecked)
                    .DisposeWith(disposables);

                this.Bind(ViewModel, vm => vm.Settings.MachineUrl, view => view.MachineUrl.Text)
                    .DisposeWith(disposables);
                

                /*
                ViewModel.WhenAnyValue(vm => vm.StatusText)
                    .BindToStrict(this, view => view.TopProgressBar.Title)
                    .DisposeWith(disposables);

                ViewModel.WhenAnyValue(vm => vm.StatusProgress)
                    .Select(d => d.Value)
                    .BindToStrict(this, view => view.TopProgressBar.ProgressPercent)
                    .DisposeWith(disposables);
                */

                ViewModel.WhenAnyValue(vm => vm.Settings.AlwaysEnabled)
                    .WhereNotNull()
                    .Select(itms => itms.Select(itm => new RemovableItemViewModel(itm.ToString(), () => ViewModel.RemoveAlwaysEnabled(itm))).ToArray())
                    .BindToStrict(this, view => view.AlwaysEnabled.ItemsSource)
                    .DisposeWith(disposables);

                AddAlwaysEnabled.Command = ReactiveCommand.CreateFromTask(async () => await AddAlwaysEnabledCommand());
                
                
                ViewModel.WhenAnyValue(vm => vm.Settings.AdditionalProfiles)
                    .WhereNotNull()
                    .Select(itms => itms.Select(itm => new RemovableItemViewModel(itm.ToString(), () => ViewModel.RemoveProfile(itm))).ToArray())
                    .BindToStrict(this, view => view.OtherProfiles.ItemsSource)
                    .DisposeWith(disposables);

                AddOtherProfile.Command = ReactiveCommand.CreateFromTask(async () => await AddOtherProfileCommand());
                
                ViewModel.WhenAnyValue(vm => vm.Settings.NoMatchInclude)
                    .WhereNotNull()
                    .Select(itms => itms.Select(itm => new RemovableItemViewModel(itm.ToString(), () => ViewModel.RemoveNoMatchInclude(itm))).ToArray())
                    .BindToStrict(this, view => view.NoMatchInclude.ItemsSource)
                    .DisposeWith(disposables);

                AddNoMatchInclude.Command = ReactiveCommand.CreateFromTask(async () => await AddNoMatchIncludeCommand());
                
                ViewModel.WhenAnyValue(vm => vm.Settings.Include)
                    .WhereNotNull()
                    .Select(itms => itms.Select(itm => new RemovableItemViewModel(itm.ToString(), () => ViewModel.RemoveInclude(itm))).ToArray())
                    .BindToStrict(this, view => view.Include.ItemsSource)
                    .DisposeWith(disposables);

                AddInclude.Command = ReactiveCommand.CreateFromTask(async () => await AddIncludeCommand());
                AddIncludeFiles.Command = ReactiveCommand.CreateFromTask(async () => await AddIncludeFilesCommand());
                
                ViewModel.WhenAnyValue(vm => vm.Settings.Ignore)
                    .WhereNotNull()
                    .Select(itms => itms.Select(itm => new RemovableItemViewModel(itm.ToString(), () => ViewModel.RemoveIgnore(itm))).ToArray())
                    .BindToStrict(this, view => view.Ignore.ItemsSource)
                    .DisposeWith(disposables);

                AddIgnore.Command = ReactiveCommand.CreateFromTask(async () => await AddIgnoreCommand());
                AddIgnoreFiles.Command = ReactiveCommand.CreateFromTask(async () => await AddIgnoreFilesCommand());


            });

        }

        public async Task AddAlwaysEnabledCommand()
        {
            AbsolutePath dirPath;

            if (ViewModel!.Settings.Source != default && ViewModel.Settings.Source.Combine("mods").DirectoryExists())
            {
                dirPath = ViewModel.Settings.Source.Combine("mods");
            }
            else
            {
                dirPath = ViewModel.Settings.Source;
            }
            
            var dlg = new CommonOpenFileDialog
            {
                Title = "Please select a folder",
                IsFolderPicker = true,
                InitialDirectory = dirPath.ToString(),
                AddToMostRecentlyUsedList = false,
                AllowNonFileSystemItems = false,
                DefaultDirectory = dirPath.ToString(),
                EnsureFileExists = true,
                EnsurePathExists = true,
                EnsureReadOnly = false,
                EnsureValidNames = true,
                Multiselect = true,
                ShowPlacesList = true,
            };

            if (dlg.ShowDialog() != CommonFileDialogResult.Ok) return;
            foreach (var fileName in dlg.FileNames)
            {
                var selectedPath = fileName.ToAbsolutePath();

                if (!selectedPath.InFolder(ViewModel.Settings.Source)) continue;

                ViewModel.AddAlwaysEnabled(selectedPath.RelativeTo(ViewModel.Settings.Source));
            }
        }
        
        public async Task AddOtherProfileCommand()
        {
            AbsolutePath dirPath;

            if (ViewModel!.Settings.Source != default && ViewModel.Settings.Source.Combine("mods").DirectoryExists())
            {
                dirPath = ViewModel.Settings.Source.Combine("mods");
            }
            else
            {
                dirPath = ViewModel.Settings.Source;
            }
            
            var dlg = new CommonOpenFileDialog
            {
                Title = "Please select a profile folder",
                IsFolderPicker = true,
                InitialDirectory = dirPath.ToString(),
                AddToMostRecentlyUsedList = false,
                AllowNonFileSystemItems = false,
                DefaultDirectory = dirPath.ToString(),
                EnsureFileExists = true,
                EnsurePathExists = true,
                EnsureReadOnly = false,
                EnsureValidNames = true,
                Multiselect = true,
                ShowPlacesList = true,
            };

            if (dlg.ShowDialog() != CommonFileDialogResult.Ok) return;
            foreach (var filename in dlg.FileNames)
            {
                var selectedPath = filename.ToAbsolutePath();
                
                if (!selectedPath.InFolder(ViewModel.Settings.Source.Combine("profiles"))) continue;
                
                ViewModel.AddOtherProfile(selectedPath.FileName.ToString());
            }
        }
        
        public Task AddNoMatchIncludeCommand()
        {
            var dlg = new CommonOpenFileDialog
            {
                Title = "Please select a folder",
                IsFolderPicker = true,
                InitialDirectory = ViewModel!.Settings.Source.ToString(),
                AddToMostRecentlyUsedList = false,
                AllowNonFileSystemItems = false,
                DefaultDirectory = ViewModel!.Settings.Source.ToString(),
                EnsureFileExists = true,
                EnsurePathExists = true,
                EnsureReadOnly = false,
                EnsureValidNames = true,
                Multiselect = true,
                ShowPlacesList = true,
            };

            if (dlg.ShowDialog() != CommonFileDialogResult.Ok) return Task.CompletedTask;
            foreach (var filename in dlg.FileNames)
            {
                var selectedPath = filename.ToAbsolutePath();

                if (!selectedPath.InFolder(ViewModel.Settings.Source)) continue;
            
                ViewModel.AddNoMatchInclude(selectedPath.RelativeTo(ViewModel!.Settings.Source));
            }
            
            return Task.CompletedTask;
        }
        
        public async Task AddIncludeCommand()
        {
            var dlg = new CommonOpenFileDialog
            {
                Title = "Please select folders to include",
                IsFolderPicker = true,
                InitialDirectory = ViewModel!.Settings.Source.ToString(),
                AddToMostRecentlyUsedList = false,
                AllowNonFileSystemItems = false,
                DefaultDirectory = ViewModel!.Settings.Source.ToString(),
                EnsureFileExists = true,
                EnsurePathExists = true,
                EnsureReadOnly = false,
                EnsureValidNames = true,
                Multiselect = true,
                ShowPlacesList = true,
            };

            if (dlg.ShowDialog() != CommonFileDialogResult.Ok) return;
            foreach (var filename in dlg.FileNames)
            {
                var selectedPath = filename.ToAbsolutePath();

                if (!selectedPath.InFolder(ViewModel.Settings.Source)) continue;
            
                ViewModel.AddInclude(selectedPath.RelativeTo(ViewModel!.Settings.Source));
            }
        }
        
        public async Task AddIncludeFilesCommand()
        {
            var dlg = new CommonOpenFileDialog
            {
                Title = "Please select files to include",
                IsFolderPicker = false,
                InitialDirectory = ViewModel!.Settings.Source.ToString(),
                AddToMostRecentlyUsedList = false,
                AllowNonFileSystemItems = false,
                DefaultDirectory = ViewModel!.Settings.Source.ToString(),
                EnsureFileExists = true,
                EnsurePathExists = true,
                EnsureReadOnly = false,
                EnsureValidNames = true,
                Multiselect = true,
                ShowPlacesList = true,
            };

            if (dlg.ShowDialog() != CommonFileDialogResult.Ok) return;
            foreach (var filename in dlg.FileNames)
            {
                var selectedPath = filename.ToAbsolutePath();

                if (!selectedPath.InFolder(ViewModel.Settings.Source)) continue;
            
                ViewModel.AddInclude(selectedPath.RelativeTo(ViewModel!.Settings.Source));
            }
        }
        
        public async Task AddIgnoreCommand()
        {
            var dlg = new CommonOpenFileDialog
            {
                Title = "Please select folders to ignore",
                IsFolderPicker = true,
                InitialDirectory = ViewModel!.Settings.Source.ToString(),
                AddToMostRecentlyUsedList = false,
                AllowNonFileSystemItems = false,
                DefaultDirectory = ViewModel!.Settings.Source.ToString(),
                EnsureFileExists = true,
                EnsurePathExists = true,
                EnsureReadOnly = false,
                EnsureValidNames = true,
                Multiselect = true,
                ShowPlacesList = true,
            };

            if (dlg.ShowDialog() != CommonFileDialogResult.Ok) return;
            foreach (var filename in dlg.FileNames)
            {
                var selectedPath = filename.ToAbsolutePath();

                if (!selectedPath.InFolder(ViewModel.Settings.Source)) continue;
            
                ViewModel.AddIgnore(selectedPath.RelativeTo(ViewModel!.Settings.Source));
            }
        }
        
        public async Task AddIgnoreFilesCommand()
        {
            var dlg = new CommonOpenFileDialog
            {
                Title = "Please select files to ignore",
                IsFolderPicker = false,
                InitialDirectory = ViewModel!.Settings.Source.ToString(),
                AddToMostRecentlyUsedList = false,
                AllowNonFileSystemItems = false,
                DefaultDirectory = ViewModel!.Settings.Source.ToString(),
                EnsureFileExists = true,
                EnsurePathExists = true,
                EnsureReadOnly = false,
                EnsureValidNames = true,
                Multiselect = true,
                ShowPlacesList = true,
            };

            if (dlg.ShowDialog() != CommonFileDialogResult.Ok) return;
            foreach (var filename in dlg.FileNames)
            {
                var selectedPath = filename.ToAbsolutePath();

                if (!selectedPath.InFolder(ViewModel.Settings.Source)) continue;
            
                ViewModel.AddIgnore(selectedPath.RelativeTo(ViewModel!.Settings.Source));
            }
        }
    }
}
