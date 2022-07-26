using System;
using System.Collections.Generic;
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
    /// Interaction logic for SettingsView.xaml
    /// </summary>
    public partial class SettingsView : ReactiveUserControl<SettingsVM>
    {
        public SettingsView()
        {
            InitializeComponent();
            this.WhenActivated(disposable =>
            {
                this.OneWayBindStrict(this.ViewModel, x => x.BackCommand, x => x.BackButton.Command)
                    .DisposeWith(disposable);
                this.OneWayBindStrict(this.ViewModel, x => x.Login, x => x.LoginView.ViewModel)
                    .DisposeWith(disposable);
                this.OneWayBindStrict(this.ViewModel, x => x.Performance, x => x.PerformanceView.ViewModel)
                    .DisposeWith(disposable);
                this.OneWayBindStrict(this.ViewModel, x => x.AuthorFile, x => x.AuthorFilesView.ViewModel)
                    .DisposeWith(disposable);
                this.MiscGalleryView.ViewModel = this.ViewModel;
            });
        }
    }
}
