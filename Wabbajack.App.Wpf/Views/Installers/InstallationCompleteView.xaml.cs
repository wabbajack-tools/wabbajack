using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;

namespace Wabbajack;

/// <summary>
/// Interaction logic for InstallationCompleteView.xaml
/// </summary>
public partial class InstallationCompleteView : ReactiveUserControl<InstallerVM>
{
    public InstallationCompleteView()
    {
        InitializeComponent();
        this.WhenActivated(dispose =>
        {
            this.WhenAny(x => x.ViewModel.InstallState)
                .Select(x => x == InstallState.Failure)
                .BindToStrict(this, x => x.AttentionBorder.Failure)
                .DisposeWith(dispose);
            this.WhenAny(x => x.ViewModel.InstallState)
                .Select(x => x == InstallState.Failure)
                .Select(failed => $"Installation {(failed ? "Failed" : "Complete")}")
                .BindToStrict(this, x => x.TitleText.Text)
                .DisposeWith(dispose);
            this.WhenAny(x => x.ViewModel.BackCommand)
                .BindToStrict(this, x => x.BackButton.Command)
                .DisposeWith(dispose);
            this.WhenAny(x => x.ViewModel.GoToInstallCommand)
                .BindToStrict(this, x => x.GoToInstallButton.Command)
                .DisposeWith(dispose);
            this.WhenAny(x => x.ViewModel.OpenReadmeCommand)
                .BindToStrict(this, x => x.OpenReadmeButton.Command)
                .DisposeWith(dispose);
            this.WhenAny(x => x.ViewModel.OpenWikiCommand)
                .BindToStrict(this, x => x.OpenWikiButton.Command)
                .DisposeWith(dispose);
            this.WhenAny(x => x.ViewModel.CloseWhenCompleteCommand)
                .BindToStrict(this, x => x.CloseButton.Command)
                .DisposeWith(dispose);
            this.WhenAny(x => x.ViewModel.OpenLogsCommand)
                .BindToStrict(this, x => x.OpenLogsButton.Command)
                .DisposeWith(dispose);
        });
    }
}
