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

namespace Wabbajack
{
    /// <summary>
    /// Interaction logic for LoginSettingsView.xaml
    /// </summary>
    public partial class LoginSettingsView : ReactiveUserControl<LoginManagerVM>
    {
        public LoginSettingsView()
        {
            InitializeComponent();
            this.WhenActivated(disposable =>
            {
                this.OneWayBindStrict(this.ViewModel, x => x.Logins, x => x.DownloadersList.ItemsSource)
                    .DisposeWith(disposable);
            });
        }
    }
}
