using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Wabbajack;

/// <summary>
/// Interaction logic for RadioButtonView.axaml (ImageRadioButtonView)
/// </summary>
public partial class ImageRadioButtonView : UserControl
{
    public static readonly StyledProperty<bool> IsCheckedProperty =
        AvaloniaProperty.Register<ImageRadioButtonView, bool>(nameof(IsChecked), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public bool IsChecked
    {
        get => GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    public static readonly StyledProperty<Bitmap?> ImageProperty =
        AvaloniaProperty.Register<ImageRadioButtonView, Bitmap?>(nameof(Image));

    public Bitmap? Image
    {
        get => GetValue(ImageProperty);
        set => SetValue(ImageProperty, value);
    }

    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<ImageRadioButtonView, ICommand?>(nameof(Command));

    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    private Image? _radioImage;

    static ImageRadioButtonView()
    {
        // Replaces the WPF Style.Triggers/MultiDataTrigger on the templated Image
        // (blur only while neither hovered nor checked): re-evaluate the blur
        // effect whenever IsChecked changes.
        IsCheckedProperty.Changed.AddClassHandler<ImageRadioButtonView>((view, _) => view.UpdateBlurEffect());
    }

    public ImageRadioButtonView()
    {
        InitializeComponent();

        var radioButton = this.FindControl<Button>("RadioButton");
        if (radioButton is not null)
        {
            radioButton.TemplateApplied += OnButtonTemplateApplied;
        }
    }

    private void OnButtonTemplateApplied(object? sender, TemplateAppliedEventArgs e)
    {
        _radioImage = e.NameScope.Find<Image>("RadioImage");
        UpdateBlurEffect();
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        UpdateBlurEffect();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        UpdateBlurEffect();
    }

    private void UpdateBlurEffect()
    {
        if (_radioImage is null)
        {
            return;
        }

        _radioImage.Effect = !IsPointerOver && !IsChecked
            ? new BlurEffect { Radius = 2 }
            : null;
    }

    private void Button_Click(object? sender, RoutedEventArgs e)
    {
        IsChecked = true;
    }
}
