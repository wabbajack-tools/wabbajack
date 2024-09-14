using System.Reactive.Disposables;
using ReactiveUI;

namespace Wabbajack;

/// <summary>
/// Interaction logic for ConfirmationInterventionView.xaml
/// </summary>
public partial class ConfirmationInterventionView : ReactiveUserControl<ConfirmationIntervention>
{
    public ConfirmationInterventionView()
    {
        InitializeComponent();
        this.WhenActivated(dispose =>
        {
            this.WhenAny(x => x.ViewModel.ShortDescription)
                .BindToStrict(this, x => x.ShortDescription.Text)
                .DisposeWith(dispose);
            this.WhenAny(x => x.ViewModel.ExtendedDescription)
                .BindToStrict(this, x => x.ExtendedDescription.Text)
                .DisposeWith(dispose);
            this.WhenAny(x => x.ViewModel.ConfirmCommand)
                .BindToStrict(this, x => x.ConfirmButton.Command)
                .DisposeWith(dispose);
            this.WhenAny(x => x.ViewModel.CancelCommand)
                .BindToStrict(this, x => x.CancelButton.Command)
                .DisposeWith(dispose);
        });
    }
}
