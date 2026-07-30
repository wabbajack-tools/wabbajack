using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.ReactiveUI;
using ReactiveUI;

namespace Wabbajack;

/// <summary>
/// Interaction logic for ContributorView.axaml
/// </summary>
public partial class ContributorView : ReactiveUserControl<ContributorVM>
{
    private Avalonia.Media.ImageBrush AvatarImage => (Avalonia.Media.ImageBrush)AvatarBorder.Background!;
    public ContributorView()
    {
        InitializeComponent();

        this.WhenActivated(disposable =>
        {
            ViewModel.WhenAnyValue(vm => vm.OpenProfileCommand)
                     .BindToStrict(this, v => v.AvatarButton.Command)
                     .DisposeWith(disposable);

            // ContributorVM.Avatar is an Avalonia.Media.Imaging.Bitmap, while
            // ImageBrush.Source is typed as the broader Avalonia.Media.IImage.
            // BindToStrict requires the source observable's value type to match
            // the bound property's type exactly, so the Bitmap is upcast to
            // IImage via Select before binding.
            ViewModel.WhenAnyValue(vm => vm.Avatar)
                     .Select(avatar => (Avalonia.Media.IImageBrushSource?)avatar)
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
