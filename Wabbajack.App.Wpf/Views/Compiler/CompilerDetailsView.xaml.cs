using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using DynamicData;
using Microsoft.WindowsAPICodePack.Dialogs;
using Wabbajack.Common;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using System.Collections.Generic;

namespace Wabbajack;

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
            this.Bind(ViewModel, vm => vm.Settings.ModListName, view => view.ModListNameSetting.Text)
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

            this.Bind(ViewModel, vm => vm.Settings.UseTextureRecompression, view => view.TextureRecompressionSetting.IsChecked)
                .DisposeWith(disposables);

            this.WhenAnyValue(v => v.ViewModel.AvailableProfiles)
                .BindToStrict(this, view => view.ProfileSetting.ItemsSource)
                .DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.Settings.Profile, view => view.ProfileSetting.SelectedItem)
                .DisposeWith(disposables);

            ViewModel.WhenAnyValue(v => v.AvailableProfiles, v => v.Settings.Profile)
                     .Select((x) => x.Item1.Except([x.Item2]).ToList())
                     .BindToStrict(this, x => x.AdditionalProfilesSetting.ItemsSource)
                     .DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.Settings.MachineUrl, view => view.MachineUrl.Text)
                .DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.DownloadLocation, view => view.DownloadsPicker.PickerVM)
                .DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Settings.Downloads, view => view.DownloadsPicker.PickerVM.TargetPath)
                .DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.OutputLocation, view => view.OutputFilePicker.PickerVM)
                .DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Settings.OutputFile, view => view.OutputFilePicker.PickerVM.TargetPath)
                .DisposeWith(disposables);
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
