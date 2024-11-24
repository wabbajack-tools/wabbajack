using FluentIcons.Common;
using ReactiveUI;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows;
using System;
using System.Windows.Input;

namespace Wabbajack;

/// <summary>
/// Interaction logic for WJButton.xaml
/// </summary>
public enum ButtonStyle
{
    Mono,
    Color,
    Danger
}
public partial class WJButton : UserControlRx<ViewModel>
{
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(WJButton),
         new FrameworkPropertyMetadata(default(string)));

    public Symbol Icon
    {
        get => (Symbol)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(Symbol), typeof(WJButton), new FrameworkPropertyMetadata(default(Symbol)));

    public double IconSize
    {
        get => (double)GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }
    public static readonly DependencyProperty IconSizeProperty = DependencyProperty.Register(nameof(IconSize), typeof(double), typeof(WJButton), new FrameworkPropertyMetadata(24D));

    public FlowDirection Direction
    {
        get => (FlowDirection)GetValue(DirectionProperty);
        set => SetValue(DirectionProperty, value);
    }
    public static readonly DependencyProperty DirectionProperty = DependencyProperty.Register(nameof(Direction), typeof(FlowDirection), typeof(WJButton), new PropertyMetadata(FlowDirection.LeftToRight));

    public ICommand Command
    {
        get => (ICommand)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }
    public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(nameof(Command), typeof(ICommand), typeof(WJButton), new PropertyMetadata(default(ReactiveCommand)));

    public ButtonStyle ButtonStyle
    {
        get => (ButtonStyle)GetValue(ButtonStyleProperty);
        set => SetValue(ButtonStyleProperty, value);
    }
    public static readonly DependencyProperty ButtonStyleProperty = DependencyProperty.Register(nameof(ButtonStyle), typeof(ButtonStyle), typeof(WJButton), new PropertyMetadata(ButtonStyle.Mono));

    public WJButton()
    {
        InitializeComponent();
        this.WhenActivated(dispose =>
        {
            this.WhenAny(x => x.Text)
                .BindToStrict(this, x => x.ButtonTextBlock.Text)
                .DisposeWith(dispose);

            this.WhenAny(x => x.Icon)
                .BindToStrict(this, x => x.ButtonSymbolIcon.Symbol)
                .DisposeWith(dispose);

            this.WhenAny(x => x.Direction)
                .Subscribe(x => SetDirection(x))
                .DisposeWith(dispose);

            this.WhenAny(x => x.Command)
                .BindToStrict(this, x => x.Button.Command)
                .DisposeWith(dispose);

            this.WhenAny(x => x.IconSize)
                .BindToStrict(this, x => x.ButtonSymbolIcon.FontSize)
                .DisposeWith(dispose);

            this.WhenAny(x => x.ButtonStyle)
                .Subscribe(x => Button.Style = x switch
                {
                    ButtonStyle.Mono => (Style)Application.Current.Resources["WJButtonStyle"],
                    ButtonStyle.Color => (Style)Application.Current.Resources["WJColorButtonStyle"],
                    ButtonStyle.Danger => (Style)Application.Current.Resources["WJDangerButtonStyle"],
                    _ => (Style)Application.Current.Resources["WJButtonStyle"],
                })
                .DisposeWith(dispose);

        });

    }
    public void SetDirection(FlowDirection direction)
    {
        if (direction == FlowDirection.LeftToRight)
        {
            ButtonTextBlock.Margin = new Thickness(16, 0, 0, 0);
            ButtonTextBlock.HorizontalAlignment = HorizontalAlignment.Left;
            ButtonSymbolIcon.Margin = new Thickness(0, 0, 16, 0);
            ButtonSymbolIcon.HorizontalAlignment = HorizontalAlignment.Right;
        }
        else
        {
            ButtonTextBlock.Margin = new Thickness(0, 0, 16, 0);
            ButtonTextBlock.HorizontalAlignment = HorizontalAlignment.Right;
            ButtonSymbolIcon.Margin = new Thickness(16, 0, 0, 0);
            ButtonSymbolIcon.HorizontalAlignment = HorizontalAlignment.Left;
        }
    }
}
