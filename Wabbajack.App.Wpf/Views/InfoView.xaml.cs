using ReactiveUI;

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
    }
}
