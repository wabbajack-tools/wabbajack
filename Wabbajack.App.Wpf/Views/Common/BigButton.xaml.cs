using FluentIcons.Common;
using ReactiveUI;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows;
using System;
using System.Windows.Input;
using System.Reactive;

namespace Wabbajack;

/// <summary>
/// Interaction logic for BigButton.xaml
/// </summary>
public partial class BigButton : UserControlRx<ViewModel>
{
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(BigButton),
         new FrameworkPropertyMetadata(default(string), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, WireNotifyPropertyChanged));

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }
    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(nameof(Description), typeof(string), typeof(BigButton),
         new FrameworkPropertyMetadata(default(string), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, WireNotifyPropertyChanged));

    public Symbol Icon
    {
        get => (Symbol)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(Symbol), typeof(BigButton), new FrameworkPropertyMetadata(default(Symbol), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, WireNotifyPropertyChanged));

    public FlowDirection Direction
    {
        get => (FlowDirection)GetValue(DirectionProperty);
        set => SetValue(DirectionProperty, value);
    }
    public static readonly DependencyProperty DirectionProperty = DependencyProperty.Register(nameof(Direction), typeof(FlowDirection), typeof(BigButton), new FrameworkPropertyMetadata(FlowDirection.LeftToRight, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, WireNotifyPropertyChanged));

    public ICommand Command
    {
        get => (ICommand)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }
    public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(nameof(Command), typeof(ICommand), typeof(BigButton), new FrameworkPropertyMetadata(default(ReactiveCommand), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, WireNotifyPropertyChanged));

    public ButtonStyle ButtonStyle
    {
        get => (ButtonStyle)GetValue(ButtonStyleProperty);
        set => SetValue(ButtonStyleProperty, value);
    }
    public static readonly DependencyProperty ButtonStyleProperty = DependencyProperty.Register(nameof(ButtonStyle), typeof(ButtonStyle), typeof(BigButton), new FrameworkPropertyMetadata(ButtonStyle.Mono, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, WireNotifyPropertyChanged));

    public BigButton()
    {
        InitializeComponent();
        this.WhenActivated(dispose =>
        {
            this.WhenAny(x => x.Title)
                .BindToStrict(this, x => x.ButtonTitleBlock.Text)
                .DisposeWith(dispose);

            this.WhenAny(x => x.Description)
                .BindToStrict(this, x => x.ButtonDescriptionBlock.Text)
                .DisposeWith(dispose);

            this.WhenAny(x => x.Icon)
                .BindToStrict(this, x => x.ButtonSymbolIcon.Symbol)
                .DisposeWith(dispose);

            this.WhenAny(x => x.Command)
                .BindToStrict(this, x => x.Button.Command)
                .DisposeWith(dispose);

            this.WhenAny(x => x.ButtonStyle)
                .Subscribe(x => Button.Style = x switch
                {
                    ButtonStyle.Mono => (Style)Application.Current.Resources["BigButtonStyle"],
                    ButtonStyle.Color => (Style)Application.Current.Resources["BigColorButtonStyle"],
                    ButtonStyle.Danger => (Style)Application.Current.Resources["BigDangerButtonStyle"],
                    _ => (Style)Application.Current.Resources["BigButtonStyle"],
                })
                .DisposeWith(dispose);

        });

    }
}
