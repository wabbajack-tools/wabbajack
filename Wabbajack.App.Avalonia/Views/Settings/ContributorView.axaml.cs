using Avalonia.ReactiveUI;
using ReactiveUI;
using Wabbajack.App.Avalonia.ViewModels.Settings;

namespace Wabbajack.App.Avalonia.Views.Settings;

public partial class ContributorView : ReactiveUserControl<ContributorVM>
{
    public ContributorView()
    {
        InitializeComponent();
        this.WhenActivated(_ => { });
    }
}
