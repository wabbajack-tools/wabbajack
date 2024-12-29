using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;

namespace Wabbajack;

/// <summary>
/// Interaction logic for ModeSelectionView.xaml
/// </summary>
public partial class HomeView : ReactiveUserControl<HomeVM>
{
    public HomeView()
    {
        InitializeComponent();
        var vm = ViewModel;
        this.WhenActivated(dispose =>
        {
            this.WhenAnyValue(x => x.ViewModel.Modlists)
                .Select(x => x?.Length.ToString() ?? "0")
                .BindToStrict(this, x => x.ModlistAmountTextBlock.Text)
                .DisposeWith(dispose);
            this.WhenAnyValue(x => x.ViewModel.Modlists)
                .Select(x => x?.GroupBy(y => y.Game).Count().ToString() ?? "0")
                .BindToStrict(this, x => x.GameAmountTextBlock.Text)
                .DisposeWith(dispose);
        });
    }
}
