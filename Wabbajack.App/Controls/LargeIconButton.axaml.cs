using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Material.Icons;
using ReactiveUI;

namespace Wabbajack.App.Controls;

public partial class LargeIconButton : UserControl, IActivatableView
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<LargeIconButton, string>(nameof(Text));


    public static readonly StyledProperty<MaterialIconKind> IconProperty =
        AvaloniaProperty.Register<LargeIconButton, MaterialIconKind>(nameof(IconProperty));

    public LargeIconButton()
    {
        InitializeComponent();
        this.WhenActivated(dispose =>
        {
            this.WhenAnyValue(x => x.Icon)
                .Where(x => x != default)
                .BindTo(IconControl, x => x.Kind)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.Text)
                .Where(x => x != default)
                .BindTo(TextBlock, x => x.Text)
                .DisposeWith(dispose);
        });
    }

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public MaterialIconKind Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }
}