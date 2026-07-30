using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.ReactiveUI;
using ReactiveUI;

namespace Wabbajack;

/// <summary>
/// Interaction logic for ContributorView.axaml
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

            // TODO: ContributorVM.Avatar is currently typed as
            // System.Windows.Media.Imaging.BitmapImage (WPF-specific). Avalonia's
            // ImageBrush.Source expects Avalonia.Media.IImage (e.g.
            // Avalonia.Media.Imaging.Bitmap). The binding below is a structurally
            // faithful port of the WPF code but will need ContributorVM updated to
            // expose an Avalonia-compatible bitmap type before this compiles/works;
            // that VM change is out of scope for this view-only port.
            ViewModel.WhenAnyValue(vm => vm.Avatar)
                     .BindToStrict(this, v => v.AvatarImage.Source)
                     .DisposeWith(disposable);

            // WPF's Button.ToolTip is a plain settable object CLR property, so the
            // original could BindToStrict straight to it. Avalonia only exposes
            // tooltips via the attached ToolTip.Tip property (no matching CLR property
            // path on Button for BindToStrict to target), so the equivalent behavior is
            // reproduced with a manual subscription that sets the attached property.
            ViewModel.WhenAnyValue(vm => vm.Contributor.Login)
                     .Subscribe(login => ToolTip.SetTip(AvatarButton, login))
                     .DisposeWith(disposable);
        });
    }
}
