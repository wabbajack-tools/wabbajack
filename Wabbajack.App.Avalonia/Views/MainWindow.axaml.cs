using Avalonia.Controls;

namespace Wabbajack.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ActivePane.Content = new TextBlock { Text = "Wabbajack (Avalonia) — Wave 0" };
    }
}
