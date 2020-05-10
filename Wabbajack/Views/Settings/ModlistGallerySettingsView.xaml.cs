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
using Wabbajack.Common;

namespace Wabbajack
{
    /// <summary>
    /// Interaction logic for ModlistGallerySettingsView.xaml
    /// </summary>
    public partial class ModlistGallerySettingsView : ReactiveUserControl<FiltersSettings>
    {
        public ModlistGallerySettingsView()
        {
            InitializeComponent();

            this.WhenActivated(disposable =>
            {
                // Bind Values
                this.Bind(this.ViewModel, x => x.IsPersistent, x => x.FilterPersistCheckBox.IsChecked)
                    .DisposeWith(disposable);
            });
        }
    }
}
