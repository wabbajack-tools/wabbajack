using System.Windows;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;

namespace Wabbajack;

/// <summary>
/// Interaction logic for ContributorView.xaml
/// </summary>
public partial class ContributorView : ReactiveUserControl<ContributorVM>
{
    public ContributorView()
    {
        InitializeComponent();

        this.WhenActivated(disposable =>
        {
            ViewModel.WhenAnyValue(vm => vm.OpenProfileCommand)
                     .BindToStrict(this, v => v.AvatarButton.Command)
                     .DisposeWith(disposable);

            ViewModel.WhenAnyValue(vm => vm.Avatar)
                     .BindToStrict(this, v => v.AvatarImage.ImageSource)
                     .DisposeWith(disposable);

            ViewModel.WhenAnyValue(vm => vm.Contributor.Login)
                     .BindToStrict(this, v => v.AvatarButton.ToolTip)
                     .DisposeWith(disposable);
        });
    }
}
