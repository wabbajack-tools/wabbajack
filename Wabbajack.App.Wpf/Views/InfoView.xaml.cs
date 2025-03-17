using ReactiveUI;
using System.Reactive.Disposables;

namespace Wabbajack;

/// <summary>
/// Interaction logic for ModeSelectionView.xaml
/// </summary>
public partial class InfoView : ReactiveUserControl<InfoVM>
{
    public InfoView()
    {
        InitializeComponent();
        var vm = ViewModel;
        this.WhenActivated(dispose =>
        {
            this.BindCommand(ViewModel, x => x.CloseCommand, x => x.PrevButton)
                .DisposeWith(dispose);
        });
    }
}
