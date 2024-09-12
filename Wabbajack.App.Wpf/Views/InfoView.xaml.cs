using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ReactiveUI;

namespace Wabbajack
{
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
                /*
                this.WhenAnyValue(x => x.ViewModel.Modlists)
                    .Select(x => x?.Length.ToString() ?? "0")
                    .BindToStrict(this, x => x.ModlistAmountTextBlock.Text)
                    .DisposeWith(dispose);
                this.WhenAnyValue(x => x.ViewModel.Modlists)
                    .Select(x => x?.GroupBy(y => y.Game).Count().ToString() ?? "0")
                    .BindToStrict(this, x => x.GameAmountTextBlock.Text)
                    .DisposeWith(dispose);
                */
            });
        }
    }
}
