using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace Wabbajack;

/// <summary>
/// Interaction logic for ImageRadioButtonView.xaml
/// </summary>
public partial class ImageRadioButtonView : UserControl
{
    public bool IsChecked
    {
        get => (bool)GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }
    public static readonly DependencyProperty IsCheckedProperty = DependencyProperty.Register(nameof(IsChecked), typeof(bool), typeof(ImageRadioButtonView),
         new FrameworkPropertyMetadata(default(bool), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public BitmapImage Image
    {
        get => (BitmapImage)GetValue(ImageProperty);
        set => SetValue(ImageProperty, value);
    }
    public static readonly DependencyProperty ImageProperty = DependencyProperty.Register(nameof(Image), typeof(BitmapImage), typeof(ImageRadioButtonView),
         new FrameworkPropertyMetadata(default(BitmapImage)));

    public ICommand Command
    {
        get => (ICommand)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }
    public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(nameof(Command), typeof(ICommand), typeof(ImageRadioButtonView),
         new FrameworkPropertyMetadata(default(ICommand)));

    public ImageRadioButtonView()
    {
        InitializeComponent();
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        IsChecked = true;
    }
}
