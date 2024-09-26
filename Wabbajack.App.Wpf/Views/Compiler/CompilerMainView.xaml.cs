using System.Linq;
using System.Reactive.Linq;
using ReactiveUI;
using Wabbajack.Common;
using Wabbajack.Paths.IO;
using System.Windows;
using System.Reactive.Disposables;

namespace Wabbajack;

/// <summary>
/// Interaction logic for CompilingView.xaml
/// </summary>
public partial class CompilerMainView : ReactiveUserControl<CompilerMainVM>
{
    public CompilerMainView()
    {
        InitializeComponent();

        this.WhenActivated(disposables =>
        {
            ViewModel.WhenAny(vm => vm.Settings.ModListImage)
                .Where(i => i.FileExists())
                .Select(i => (UIUtils.TryGetBitmapImageFromFile(i, out var img), img))
                .Where(i => i.Item1)
                .Select(i => i.img)
                .BindToStrict(this, view => view.DetailImage.Image)
                .DisposeWith(disposables);

            ViewModel.WhenAny(vm => vm.Settings.ModListName)
                .BindToStrict(this, view => view.DetailImage.Title)
                .DisposeWith(disposables);

            ViewModel.WhenAny(vm => vm.Settings.ModListAuthor)
                .BindToStrict(this, view => view.DetailImage.Author)
                .DisposeWith(disposables);

            ViewModel.WhenAny(x => x.State)
                     .Select(s => s == CompilerState.Configuration ? Visibility.Visible : Visibility.Hidden)
                     .BindToStrict(this, x => x.ConfigurationView.Visibility)
                     .DisposeWith(disposables);

            ViewModel.WhenAny(vm => vm.State)
                    .Select(s => s == CompilerState.Configuration ? Visibility.Visible : Visibility.Hidden)
                    .BindToStrict(this, view => view.ConfigurationButtons.Visibility)
                    .DisposeWith(disposables);

            ViewModel.WhenAny(x => x.State)
                     .Select(s => s == CompilerState.Compiling || s == CompilerState.Errored ? Visibility.Visible : Visibility.Hidden)
                     .BindToStrict(this, x => x.LogView.Visibility)
                     .DisposeWith(disposables);

            ViewModel.WhenAny(x => x.State)
                     .Select(s => s == CompilerState.Compiling ? Visibility.Visible : Visibility.Hidden)
                     .BindToStrict(this, x => x.CpuView.Visibility)
                     .DisposeWith(disposables);

            ViewModel.WhenAny(vm => vm.State)
                    .Select(s => s == CompilerState.Compiling ? Visibility.Visible : Visibility.Hidden)
                    .BindToStrict(this, view => view.CompilationButtons.Visibility)
                    .DisposeWith(disposables);
        });
    }
}
