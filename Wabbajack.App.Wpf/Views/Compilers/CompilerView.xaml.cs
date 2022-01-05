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
                
                UserInterventionsControl.Visibility = Visibility.Collapsed;



            });

        }
    }
}
