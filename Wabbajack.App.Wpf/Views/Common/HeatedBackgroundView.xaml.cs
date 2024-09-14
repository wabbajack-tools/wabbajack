using System.Windows;
using System.Windows.Controls;

namespace Wabbajack;

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
