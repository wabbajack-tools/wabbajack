using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
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
using Wabbajack;

namespace Wabbajack
{
    /// <summary>
    /// Interaction logic for ConfirmUpdateOfExistingInstallView.xaml
    /// </summary>
    public partial class ConfirmUpdateOfExistingInstallView : ReactiveUserControl<ConfirmUpdateOfExistingInstallVM>
    {
        public ConfirmUpdateOfExistingInstallView()
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
                this.WhenAny(x => x.ViewModel.Source.ConfirmCommand)
                    .BindToStrict(this, x => x.ConfirmButton.Command)
                    .DisposeWith(dispose);
                this.WhenAny(x => x.ViewModel.Source.CancelCommand)
                    .BindToStrict(this, x => x.CancelButton.Command)
                    .DisposeWith(dispose);
            });
        }
    }
}
