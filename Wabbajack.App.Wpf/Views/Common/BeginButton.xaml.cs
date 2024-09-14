using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Wabbajack;

/// <summary>
/// Interaction logic for BeginButton.xaml
/// </summary>
public partial class BeginButton : UserControl
{
    public ICommand Command
    {
        get => (ICommand)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }
    public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(nameof(Command), typeof(ICommand), typeof(BeginButton),
         new FrameworkPropertyMetadata(default(ICommand)));

    public BeginButton()
    {
        InitializeComponent();
    }
}
