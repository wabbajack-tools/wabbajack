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
    /// Interaction logic for HeatedBackgroundView.xaml
    /// </summary>
    public partial class HeatedBackgroundView : UserControl
    {
        public double PercentCompleted
        {
            get => (double)GetValue(PercentCompletedProperty);
            set => SetValue(PercentCompletedProperty, value);
        }
        public static readonly DependencyProperty PercentCompletedProperty = DependencyProperty.Register(nameof(PercentCompleted), typeof(double), typeof(HeatedBackgroundView),
             new FrameworkPropertyMetadata(default(double)));

        public HeatedBackgroundView()
        {
            InitializeComponent();
        }
    }
}
