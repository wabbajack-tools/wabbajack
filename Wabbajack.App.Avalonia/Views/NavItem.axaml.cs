using Avalonia;
using Avalonia.Controls;
using FluentIcons.Common;

namespace Wabbajack.App.Avalonia.Views;

public partial class NavItem : UserControl
{
    public static readonly StyledProperty<Symbol> SymbolProperty =
        AvaloniaProperty.Register<NavItem, Symbol>(nameof(Symbol));

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<NavItem, string>(nameof(Text), "");

    public NavItem() => InitializeComponent();

    public Symbol Symbol
    {
        get => GetValue(SymbolProperty);
        set => SetValue(SymbolProperty, value);
    }

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
}
