using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Controls;
using ReactiveUI;
using System.Windows;
using DynamicData;
using Wabbajack.Common;

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

                
                this.BindCommand(ViewModel, vm => vm.ExecuteCommand, view => view.BeginButton)
                    .DisposeWith(disposables);
                
                ViewModel.WhenAnyValue(vm => vm.BackCommand)
                    .BindToStrict(this, view => view.BackButton.Command)
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
                    .Select(v => v == CompilerState.Completed ? Visibility.Visible : Visibility.Collapsed)
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
                
                
                // Settings 
                
                this.Bind(ViewModel, vm => vm.ModListName, view => view.ModListNameSetting.Text)
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


            });

        }
    }
}
