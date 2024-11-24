using FluentIcons.Common;
using ReactiveUI;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows;
using System;
using System.Windows.Input;
using Wabbajack.RateLimiter;
using System.Windows.Media;
using ReactiveUI.Fody.Helpers;
using System.Windows.Controls;
using System.ComponentModel;

namespace Wabbajack;

/// <summary>
/// Interaction logic for WJButton.xaml
/// </summary>
public enum ButtonStyle
{
    Mono,
    Color,
    Danger,
    Progress
}
public partial class WJButtonVM : ViewModel
{
}

public partial class WJButton : Button, IViewFor<WJButtonVM>, IReactiveObject
{
    private string _text;

    public event PropertyChangedEventHandler PropertyChanged;
    public event PropertyChangingEventHandler PropertyChanging;

    public string Text
    {
        get => _text;
        set
        {
            this.RaiseAndSetIfChanged(ref _text, value);
            RaisePropertyChanged(new PropertyChangedEventArgs(nameof(Content)));
        }
    }
    [Reactive] public Symbol Icon { get; set; }
    [Reactive] public double IconSize { get; set; } = 24D;
    [Reactive] public FlowDirection Direction { get; set; }
    [Reactive] public ButtonStyle ButtonStyle { get; set; }
    [Reactive] public Percent ProgressPercentage { get; set; } = Percent.One;
    public WJButtonVM ViewModel { get; set; }
    object IViewFor.ViewModel { get => ViewModel; set => ViewModel = (WJButtonVM)value; }

    public WJButton()
    {
        InitializeComponent();
        this.WhenActivated(dispose =>
        {
            this.WhenAnyValue(x => x.Text)
                .BindToStrict(this, x => x.ButtonTextBlock.Text)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.Icon)
                .BindToStrict(this, x => x.ButtonSymbolIcon.Symbol)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.Direction)
                .Subscribe(x => SetDirection(x))
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.IconSize)
                .BindToStrict(this, x => x.ButtonSymbolIcon.FontSize)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ButtonStyle)
                .Subscribe(x => Style = x switch
                {
                    ButtonStyle.Mono => (Style)Application.Current.Resources["WJButtonStyle"],
                    ButtonStyle.Color => (Style)Application.Current.Resources["WJColorButtonStyle"],
                    ButtonStyle.Danger => (Style)Application.Current.Resources["WJDangerButtonStyle"],
                    ButtonStyle.Progress => (Style)Application.Current.Resources["WJProgressButtonStyle"],
                    _ => (Style)Application.Current.Resources["WJButtonStyle"],
                })
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.ProgressPercentage)
            .Subscribe(x =>
            {
                int i = 0;
            }).DisposeWith(dispose);
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

    public void RaisePropertyChanging(PropertyChangingEventArgs args)
    {
        PropertyChanging?.Invoke(this, args);
    }

    public void RaisePropertyChanged(PropertyChangedEventArgs args)
    {
        PropertyChanged?.Invoke(this, args);
    }
}
