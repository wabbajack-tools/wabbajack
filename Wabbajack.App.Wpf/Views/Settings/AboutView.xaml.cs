using System.Windows;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;

namespace Wabbajack;

/// <summary>
/// Interaction logic for AboutView.xaml
/// </summary>
public partial class AboutView : ReactiveUserControl<AboutVM>
{
    public AboutView()
    {
        InitializeComponent();

        this.WhenActivated(disposable =>
        {
            ViewModel.WhenAnyValue(vm => vm.Contributors)
                     .BindToStrict(this, v => v.ContributorsControl.ItemsSource)
                     .DisposeWith(disposable);
        });
    }
}
