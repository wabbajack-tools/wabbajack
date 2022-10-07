using System.Windows;
using System.Windows.Controls;

namespace Wabbajack
{
    /// <summary>
    /// Interaction logic for LogView.xaml
    /// </summary>
    public partial class LogView : UserControl
    {
        public double ProgressPercent
        {
            get => (double)GetValue(ProgressPercentProperty);
            set => SetValue(ProgressPercentProperty, value);
        }
        public static readonly DependencyProperty ProgressPercentProperty = DependencyProperty.Register(nameof(ProgressPercent), typeof(double), typeof(LogView),
             new FrameworkPropertyMetadata(default(double)));

        public LogView()
        {
            InitializeComponent();
        }
    }
}
