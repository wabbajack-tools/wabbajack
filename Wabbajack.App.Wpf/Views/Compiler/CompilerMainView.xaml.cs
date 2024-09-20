using System.Linq;
using System.Reactive.Linq;
using ReactiveUI;
using Wabbajack.Common;
using Wabbajack.Paths.IO;
using System.Windows;
using System.Reactive.Disposables;

namespace Wabbajack;

/// <summary>
/// Interaction logic for CompilerMainView.xaml
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

            ViewModel.WhenAny(vm => vm.State)
                    .Select(s => s == CompilerState.Configuration ? Visibility.Visible : Visibility.Hidden)
                    .BindToStrict(this, view => view.StartButton.Visibility);

            ViewModel.WhenAny(vm => vm.State)
                    .Select(s => s == CompilerState.Configuration ? Visibility.Visible : Visibility.Hidden)
                    .BindToStrict(this, view => view.PrevButton.Visibility);

            ViewModel.WhenAny(vm => vm.State)
                    .Select(s => s == CompilerState.Compiling ? Visibility.Visible : Visibility.Hidden)
                    .BindToStrict(this, view => view.CancelButton.Visibility);

            ViewModel.WhenAny(vm => vm.State)
                    .Select(s => s == CompilerState.Completed ? Visibility.Visible : Visibility.Hidden)
                    .BindToStrict(this, view => view.PublishButton.Visibility);

            ViewModel.WhenAny(vm => vm.State)
                    .Select(s => s == CompilerState.Completed ? Visibility.Visible : Visibility.Hidden)
                    .BindToStrict(this, view => view.OpenFolderButton.Visibility);
        });

    }
}
