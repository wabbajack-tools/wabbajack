using Avalonia;
using Avalonia.Media;
using Avalonia.ReactiveUI;
using Avalonia.Styling;
using FluentIcons.Common;
using ReactiveUI;
using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Input;

namespace Wabbajack;

/// <summary>
/// Interaction logic for BigButton.axaml
/// </summary>
public partial class BigButton : ReactiveUserControl<ViewModel>
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<BigButton, string>(nameof(Title), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);
    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly StyledProperty<string> DescriptionProperty =
        AvaloniaProperty.Register<BigButton, string>(nameof(Description), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);
    public string Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public static readonly StyledProperty<Symbol> IconProperty =
        AvaloniaProperty.Register<BigButton, Symbol>(nameof(Icon), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);
    public Symbol Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    // Avalonia.Media.FlowDirection mirrors WPF's System.Windows.FlowDirection (LeftToRight/RightToLeft).
    public static readonly StyledProperty<FlowDirection> DirectionProperty =
        AvaloniaProperty.Register<BigButton, FlowDirection>(nameof(Direction), FlowDirection.LeftToRight, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);
    public FlowDirection Direction
    {
        get => GetValue(DirectionProperty);
        set => SetValue(DirectionProperty, value);
    }

    public static readonly StyledProperty<ICommand> CommandProperty =
        AvaloniaProperty.Register<BigButton, ICommand>(nameof(Command), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);
    public ICommand Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    // ButtonStyle is defined in WJButton.xaml.cs (namespace Wabbajack); see TODOs.
    public static readonly StyledProperty<ButtonStyle> ButtonStyleProperty =
        AvaloniaProperty.Register<BigButton, ButtonStyle>(nameof(ButtonStyle), ButtonStyle.Mono, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);
    public ButtonStyle ButtonStyle
    {
        get => GetValue(ButtonStyleProperty);
        set => SetValue(ButtonStyleProperty, value);
    }

    public BigButton()
    {
        InitializeComponent();
        this.WhenActivated(dispose =>
        {
            this.WhenAnyValue(x => x.Title)
                .BindToStrict(this, x => x.ButtonTitleBlock.Text)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.Description)
                .BindToStrict(this, x => x.ButtonDescriptionBlock.Text)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.Icon)
                .BindToStrict(this, x => x.ButtonSymbolIcon.Symbol)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.Command)
                .BindToStrict(this, x => x.Button.Command)
                .DisposeWith(dispose);

            // WPF swapped a keyed Style here. The Avalonia port expresses those as style classes in
            // Themes/Controls.axaml, so set the class rather than looking up a ControlTheme - the
            // ControlTheme lookup silently found nothing and left every BigButton on Fluent's default.
            // (WPF never defined a BigDangerButtonStyle either; Danger falls back to the mono look.)
            this.WhenAnyValue(x => x.ButtonStyle)
                .Subscribe(x =>
                {
                    var wanted = x == ButtonStyle.Color ? "BigColorButtonStyle" : "BigButtonStyle";
                    Button.Classes.Remove(wanted == "BigButtonStyle" ? "BigColorButtonStyle" : "BigButtonStyle");
                    if (!Button.Classes.Contains(wanted)) Button.Classes.Add(wanted);
                })
                .DisposeWith(dispose);

        });

    }
}
