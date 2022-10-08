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
using Wabbajack.View_Models.Controls;

namespace Wabbajack
{
    /// <summary>
    /// Interaction logic for CompilerView.xaml
    /// </summary>
    public partial class CompilerView : ReactiveUserControl<CompilerVM>
    {
        public CompilerView()
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

                ViewModel.WhenAny(vm => vm.ModListImagePath.TargetPath)
                    .Where(i => i.FileExists())
                    .Select(i => (UIUtils.TryGetBitmapImageFromFile(i, out var img), img))
                    .Where(i => i.Item1)
                    .Select(i => i.img)
                    .BindToStrict(this, view => view.DetailImage.Image);

                ViewModel.WhenAny(vm => vm.ModListName)
                    .BindToStrict(this, view => view.DetailImage.Title);
                
                ViewModel.WhenAny(vm => vm.Author)
                    .BindToStrict(this, view => view.DetailImage.Author);

                ViewModel.WhenAny(vm => vm.Description)
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
                
                ViewModel.WhenAnyValue(vm => vm.BackCommand)
                    .BindToStrict(this, view => view.BackButton.Command)
                    .DisposeWith(disposables);
                
                ViewModel.WhenAnyValue(vm => vm.ReInferSettingsCommand)
                    .BindToStrict(this, view => view.ReInferSettings.Command)
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
                
                ViewModel.WhenAnyValue(vm => vm.OutputLocation)
                    .BindToStrict(this, view => view.CompilerConfigView.OutputLocation.PickerVM)
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
                
                this.Bind(ViewModel, vm => vm.ModListName, view => view.ModListNameSetting.Text)
                    .DisposeWith(disposables);
                
                this.Bind(ViewModel, vm => vm.SelectedProfile, view => view.SelectedProfile.Text)
                    .DisposeWith(disposables);
                
                this.Bind(ViewModel, vm => vm.Author, view => view.AuthorNameSetting.Text)
                    .DisposeWith(disposables);
                
                this.Bind(ViewModel, vm => vm.Version, view => view.VersionSetting.Text)
                    .DisposeWith(disposables);

                this.Bind(ViewModel, vm => vm.Description, view => view.DescriptionSetting.Text)
                    .DisposeWith(disposables);

                
                this.Bind(ViewModel, vm => vm.ModListImagePath, view => view.ImageFilePicker.PickerVM)
                    .DisposeWith(disposables);
                
                this.Bind(ViewModel, vm => vm.Website, view => view.WebsiteSetting.Text)
                    .DisposeWith(disposables);
                
                this.Bind(ViewModel, vm => vm.Readme, view => view.ReadmeSetting.Text)
                    .DisposeWith(disposables);
                
                this.Bind(ViewModel, vm => vm.IsNSFW, view => view.NSFWSetting.IsChecked)
                    .DisposeWith(disposables);
                
                this.Bind(ViewModel, vm => vm.PublishUpdate, view => view.PublishUpdate.IsChecked)
                    .DisposeWith(disposables);

                this.Bind(ViewModel, vm => vm.MachineUrl, view => view.MachineUrl.Text)
                    .DisposeWith(disposables);
                

                ViewModel.WhenAnyValue(vm => vm.StatusText)
                    .BindToStrict(this, view => view.TopProgressBar.Title)
                    .DisposeWith(disposables);

                ViewModel.WhenAnyValue(vm => vm.StatusProgress)
                    .Select(d => d.Value)
                    .BindToStrict(this, view => view.TopProgressBar.ProgressPercent)
                    .DisposeWith(disposables);

                ViewModel.WhenAnyValue(vm => vm.AlwaysEnabled)
                    .WhereNotNull()
                    .Select(itms => itms.Select(itm => new RemovableItemViewModel(itm.ToString(), () => ViewModel.RemoveAlwaysEnabled(itm))).ToArray())
                    .BindToStrict(this, view => view.AlwaysEnabled.ItemsSource)
                    .DisposeWith(disposables);

                AddAlwaysEnabled.Command = ReactiveCommand.CreateFromTask(async () => await AddAlwaysEnabledCommand());
                
                
                ViewModel.WhenAnyValue(vm => vm.OtherProfiles)
                    .WhereNotNull()
                    .Select(itms => itms.Select(itm => new RemovableItemViewModel(itm.ToString(), () => ViewModel.RemoveProfile(itm))).ToArray())
                    .BindToStrict(this, view => view.OtherProfiles.ItemsSource)
                    .DisposeWith(disposables);

                AddOtherProfile.Command = ReactiveCommand.CreateFromTask(async () => await AddOtherProfileCommand());
                
                ViewModel.WhenAnyValue(vm => vm.NoMatchInclude)
                    .WhereNotNull()
                    .Select(itms => itms.Select(itm => new RemovableItemViewModel(itm.ToString(), () => ViewModel.RemoveNoMatchInclude(itm))).ToArray())
                    .BindToStrict(this, view => view.NoMatchInclude.ItemsSource)
                    .DisposeWith(disposables);

                AddNoMatchInclude.Command = ReactiveCommand.CreateFromTask(async () => await AddNoMatchIncludeCommand());
                
                ViewModel.WhenAnyValue(vm => vm.Include)
                    .WhereNotNull()
                    .Select(itms => itms.Select(itm => new RemovableItemViewModel(itm.ToString(), () => ViewModel.RemoveInclude(itm))).ToArray())
                    .BindToStrict(this, view => view.Include.ItemsSource)
                    .DisposeWith(disposables);

                AddInclude.Command = ReactiveCommand.CreateFromTask(async () => await AddIncludeCommand());
                
                ViewModel.WhenAnyValue(vm => vm.Ignore)
                    .WhereNotNull()
                    .Select(itms => itms.Select(itm => new RemovableItemViewModel(itm.ToString(), () => ViewModel.RemoveIgnore(itm))).ToArray())
                    .BindToStrict(this, view => view.Ignore.ItemsSource)
                    .DisposeWith(disposables);

                AddIgnore.Command = ReactiveCommand.CreateFromTask(async () => await AddIgnoreCommand());


            });

        }

        public async Task AddAlwaysEnabledCommand()
        {
            AbsolutePath dirPath;

            if (ViewModel!.Source != default && ViewModel.Source.Combine("mods").DirectoryExists())
            {
                dirPath = ViewModel.Source.Combine("mods");
            }
            else
            {
                dirPath = ViewModel.Source;
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
                Multiselect = false,
                ShowPlacesList = true,
            };

            if (dlg.ShowDialog() != CommonFileDialogResult.Ok) return;
            var selectedPath = dlg.FileNames.First().ToAbsolutePath();

            if (!selectedPath.InFolder(ViewModel.Source)) return;
            
            ViewModel.AddAlwaysEnabled(selectedPath.RelativeTo(ViewModel.Source));
        }
        
        public async Task AddOtherProfileCommand()
        {
            AbsolutePath dirPath;

            if (ViewModel!.Source != default && ViewModel.Source.Combine("mods").DirectoryExists())
            {
                dirPath = ViewModel.Source.Combine("mods");
            }
            else
            {
                dirPath = ViewModel.Source;
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
                Multiselect = false,
                ShowPlacesList = true,
            };

            if (dlg.ShowDialog() != CommonFileDialogResult.Ok) return;
            var selectedPath = dlg.FileNames.First().ToAbsolutePath();

            if (!selectedPath.InFolder(ViewModel.Source.Combine("profiles"))) return;
            
            ViewModel.AddOtherProfile(selectedPath.FileName.ToString());
        }
        
        public Task AddNoMatchIncludeCommand()
        {
            var dlg = new CommonOpenFileDialog
            {
                Title = "Please select a folder",
                IsFolderPicker = true,
                InitialDirectory = ViewModel!.Source.ToString(),
                AddToMostRecentlyUsedList = false,
                AllowNonFileSystemItems = false,
                DefaultDirectory = ViewModel!.Source.ToString(),
                EnsureFileExists = true,
                EnsurePathExists = true,
                EnsureReadOnly = false,
                EnsureValidNames = true,
                Multiselect = false,
                ShowPlacesList = true,
            };

            if (dlg.ShowDialog() != CommonFileDialogResult.Ok) return Task.CompletedTask;
            var selectedPath = dlg.FileNames.First().ToAbsolutePath();

            if (!selectedPath.InFolder(ViewModel.Source)) return Task.CompletedTask;
            
            ViewModel.AddNoMatchInclude(selectedPath.RelativeTo(ViewModel!.Source));
            return Task.CompletedTask;
        }
        
        public async Task AddIncludeCommand()
        {
            var dlg = new CommonOpenFileDialog
            {
                Title = "Please select a file to include",
                IsFolderPicker = true,
                InitialDirectory = ViewModel!.Source.ToString(),
                AddToMostRecentlyUsedList = false,
                AllowNonFileSystemItems = false,
                DefaultDirectory = ViewModel!.Source.ToString(),
                EnsureFileExists = true,
                EnsurePathExists = true,
                EnsureReadOnly = false,
                EnsureValidNames = true,
                Multiselect = false,
                ShowPlacesList = true,
            };

            if (dlg.ShowDialog() != CommonFileDialogResult.Ok) return;
            var selectedPath = dlg.FileNames.First().ToAbsolutePath();

            if (!selectedPath.InFolder(ViewModel.Source)) return;
            
            ViewModel.AddInclude(selectedPath.RelativeTo(ViewModel!.Source));
        }
        
        public async Task AddIgnoreCommand()
        {
            var dlg = new CommonOpenFileDialog
            {
                Title = "Please select a file to ignore",
                IsFolderPicker = true,
                InitialDirectory = ViewModel!.Source.ToString(),
                AddToMostRecentlyUsedList = false,
                AllowNonFileSystemItems = false,
                DefaultDirectory = ViewModel!.Source.ToString(),
                EnsureFileExists = true,
                EnsurePathExists = true,
                EnsureReadOnly = false,
                EnsureValidNames = true,
                Multiselect = false,
                ShowPlacesList = true,
            };

            if (dlg.ShowDialog() != CommonFileDialogResult.Ok) return;
            var selectedPath = dlg.FileNames.First().ToAbsolutePath();

            if (!selectedPath.InFolder(ViewModel.Source)) return;
            
            ViewModel.AddIgnore(selectedPath.RelativeTo(ViewModel!.Source));
        }
    }
}
