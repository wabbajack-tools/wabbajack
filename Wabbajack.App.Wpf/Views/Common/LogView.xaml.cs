using Microsoft.Extensions.Logging;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using static Wabbajack.Models.LogStream;

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

        private void FilteredItems_OnFilter(object sender, FilterEventArgs e)
        {
            var item = e.Item as ILogMessage;
            e.Accepted = item.Level.Ordinal > 4;
        }

        public LogView()
        {
            InitializeComponent();
        }
    }
}
