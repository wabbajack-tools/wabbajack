using System.Windows;
using System.Windows.Controls;

namespace Wabbajack;

/// <summary>
/// Interaction logic for AttentionBorder.xaml
/// </summary>
public partial class AttentionBorder : UserControl
{
    public bool Failure
    {
        get => (bool)GetValue(FailureProperty);
        set => SetValue(FailureProperty, value);
    }
    public static readonly DependencyProperty FailureProperty = DependencyProperty.Register(nameof(Failure), typeof(bool), typeof(AttentionBorder),
         new FrameworkPropertyMetadata(default(bool)));
}
