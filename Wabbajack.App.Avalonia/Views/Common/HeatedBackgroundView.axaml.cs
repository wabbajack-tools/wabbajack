using Avalonia;
using Avalonia.Controls;

namespace Wabbajack;

/// <summary>
/// Interaction logic for HeatedBackgroundView.xaml
/// </summary>
public partial class HeatedBackgroundView : UserControl
{
    public static readonly StyledProperty<double> PercentCompletedProperty =
        AvaloniaProperty.Register<HeatedBackgroundView, double>(nameof(PercentCompleted), defaultValue: default(double));

    public double PercentCompleted
    {
        get => GetValue(PercentCompletedProperty);
        set => SetValue(PercentCompletedProperty, value);
    }

    public HeatedBackgroundView()
    {
        InitializeComponent();
    }
}
