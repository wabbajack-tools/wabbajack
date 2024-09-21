using FluentIcons.Common;
using ReactiveUI;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows;
using System;
using System.Windows.Input;

namespace Wabbajack;

/// <summary>
/// Interaction logic for WizardButton.xaml
/// </summary>
public partial class WizardButton : UserControlRx<ViewModel>
{
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(WizardButton),
         new FrameworkPropertyMetadata(default(string)));

    public Symbol Icon
    {
        get => (Symbol)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(Symbol), typeof(WizardButton), new FrameworkPropertyMetadata(default(Symbol)));

    public FlowDirection Direction
    {
        get => (FlowDirection)GetValue(DirectionProperty);
        set => SetValue(DirectionProperty, value);
    }
    public static readonly DependencyProperty DirectionProperty = DependencyProperty.Register(nameof(Direction), typeof(FlowDirection), typeof(WizardButton), new PropertyMetadata(FlowDirection.LeftToRight));

    public ICommand Command
    {
        get => (ICommand)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }
    public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(nameof(Command), typeof(ICommand), typeof(WizardButton), new PropertyMetadata(default(ReactiveCommand)));

    public bool UseAltStyle
    {
        get => (bool)GetValue(UseAltStyleProperty);
        set => SetValue(UseAltStyleProperty, value);
    }
    public static readonly DependencyProperty UseAltStyleProperty = DependencyProperty.Register(nameof(UseAltStyle), typeof(bool), typeof(WizardButton), new PropertyMetadata(false));

    public WizardButton()
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

            this.WhenAny(x => x.UseAltStyle)
                .Subscribe(x => Button.Style = x ? (Style)Application.Current.Resources["WizardAltButtonStyle"] : Button.Style = (Style)Application.Current.Resources["WizardButtonStyle"])
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
