using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows;
using ReactiveUI;

namespace Wabbajack;

/// <summary>
/// Interaction logic for BulkDownloadsView.xaml
/// </summary>
public partial class BulkDownloadsView : ReactiveUserControl<BulkDownloadsVM>
{
    public BulkDownloadsView()
    {
        InitializeComponent();
        this.WhenActivated(dispose =>
        {
            this.OneWayBind(ViewModel, vm => vm.Rows, v => v.RowsList.ItemsSource)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel.FooterText)
                .BindToStrict(this, x => x.FooterText.Text)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ViewModel.IsDownloading)
                .Select(downloading => downloading ? Visibility.Visible : Visibility.Collapsed)
                .BindToStrict(this, x => x.StopButton.Visibility)
                .DisposeWith(dispose);

            this.BindCommand(ViewModel, vm => vm.StopDownloadsCommand, v => v.StopButton)
                .DisposeWith(dispose);
        });
    }
}
