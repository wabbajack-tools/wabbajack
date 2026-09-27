using System.Reactive.Disposables;
using Avalonia.ReactiveUI;
using ReactiveUI;
using Wabbajack.App.Avalonia.ViewModels.Common;

namespace Wabbajack.App.Avalonia.Views.Common;

/// <summary>
/// The WPF CpuView. Its ViewModel follows the DataContext, which is how WPF's <c>ViewModel="{Binding}"</c>
/// was used: place it where the installer or compiler view model is the DataContext.
/// </summary>
public partial class CpuView : ReactiveUserControl<ICpuStatusVM>
{
    public CpuView()
    {
        InitializeComponent();
        this.WhenActivated(disposable =>
        {
            this.WhenAnyValue(x => x.ViewModel!.StatusList)
                .BindTo(this, x => x.CpuListControl.ItemsSource)
                .DisposeWith(disposable);
        });
    }
}
