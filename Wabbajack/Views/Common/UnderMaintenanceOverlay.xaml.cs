using System;
using System.Collections.Generic;
using System.Linq;
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

namespace Wabbajack
{
    /// <summary>
    /// Interaction logic for UnderMaintenanceOverlay.xaml
    /// </summary>
    public partial class UnderMaintenanceOverlay : UserControl
    {
        public bool ShowHelp
        {
            get => (bool)GetValue(ShowHelpProperty);
            set => SetValue(ShowHelpProperty, value);
        }
        public static readonly DependencyProperty ShowHelpProperty = DependencyProperty.Register(nameof(ShowHelp), typeof(bool), typeof(UnderMaintenanceOverlay),
             new FrameworkPropertyMetadata(default(bool)));

        public UnderMaintenanceOverlay()
        {
            InitializeComponent();
        }

        private void Help_Click(object sender, RoutedEventArgs e)
        {
            ShowHelp = !ShowHelp;
        }
    }
}
