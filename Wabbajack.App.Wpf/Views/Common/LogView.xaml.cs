using System.Windows.Controls;
using static Wabbajack.Models.LogStream;

namespace Wabbajack;

/// <summary>
/// Interaction logic for LogView.xaml
/// </summary>
public partial class LogView : UserControl
{
    public LogView()
    {
        InitializeComponent();
    }

    private void CollectionViewSource_Filter(object sender, System.Windows.Data.FilterEventArgs e)
    {
        var row = e.Item as ILogMessage;
        e.Accepted = row.Level.Ordinal >= 2;
    }
}
