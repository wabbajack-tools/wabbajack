using System.Windows;
using System.Windows.Controls;

namespace Wabbajack
{
    /// <summary>
    /// Interaction logic for LogCpuView.xaml
    /// </summary>
    public partial class LogCpuView : UserControl
    {
        public double ProgressPercent
        {
            get => (double)GetValue(ProgressPercentProperty);
            set => SetValue(ProgressPercentProperty, value);
        }
        public static readonly DependencyProperty ProgressPercentProperty = DependencyProperty.Register(nameof(ProgressPercent), typeof(double), typeof(LogCpuView),
             new FrameworkPropertyMetadata(default(double)));

        public LogCpuView()
        {
            InitializeComponent();
        }
    }
}
