using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using FluentIcons.Common;
using ReactiveUI;
using Wabbajack.App.Avalonia.ViewModels.Common;
using Wabbajack.Paths;

namespace Wabbajack.App.Avalonia.Views.Common;

/// <summary>
/// The WPF FilePicker: a text box and browse button over a <see cref="FilePickerVM" />, which it reads from
/// <see cref="PickerVM" /> rather than the DataContext, as WPF did. <see cref="Icon" /> is the button's symbol
/// and <see cref="Watermark" /> the empty box's hint.
/// </summary>
public partial class FilePicker : UserControl, IActivatableView
{
    public static readonly StyledProperty<FilePickerVM?> PickerVMProperty =
        AvaloniaProperty.Register<FilePicker, FilePickerVM?>(nameof(PickerVM));

    public static readonly StyledProperty<Symbol> IconProperty =
        AvaloniaProperty.Register<FilePicker, Symbol>(nameof(Icon));

    public static readonly StyledProperty<string?> WatermarkProperty =
        AvaloniaProperty.Register<FilePicker, string?>(nameof(Watermark));

    public FilePicker()
    {
        InitializeComponent();
        IconView.Symbol = Icon;

        this.WhenActivated(dispose =>
        {
            this.WhenAnyValue(x => x.PickerVM!.SetTargetPathCommand)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(cmd => SetTargetPathButton.Command = cmd)
                .DisposeWith(dispose);

            this.WhenAnyValue(x => x.PickerVM!.InError)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(inError => Classes.Set("inError", inError))
                .DisposeWith(dispose);

            // The WPF binding was two-way on every keystroke through AbsolutePathToStringConverter. The box is
            // only rewritten when the path it holds differs, so normalising a path does not move the caret.
            this.WhenAnyValue(x => x.PickerVM!.TargetPath)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(path =>
                {
                    if (AbsolutePath.ConvertNoFailure(PathBox.Text ?? string.Empty) != path)
                        PathBox.Text = path.ToString();
                })
                .DisposeWith(dispose);

            Observable.FromEventPattern<TextChangedEventArgs>(h => PathBox.TextChanged += h, h => PathBox.TextChanged -= h)
                .Subscribe(_ =>
                {
                    if (PickerVM is not { } vm) return;
                    var path = AbsolutePath.ConvertNoFailure(PathBox.Text ?? string.Empty);
                    if (vm.TargetPath != path) vm.TargetPath = path;
                })
                .DisposeWith(dispose);
        });
    }

    // This exists, as utilizing the datacontext directly seemed to bug out the exit animations in WPF;
    // kept so hosts bind the same way.
    public FilePickerVM? PickerVM
    {
        get => GetValue(PickerVMProperty);
        set => SetValue(PickerVMProperty, value);
    }

    public Symbol Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string? Watermark
    {
        get => GetValue(WatermarkProperty);
        set => SetValue(WatermarkProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IconProperty)
            IconView.Symbol = Icon;
        else if (change.Property == WatermarkProperty)
            PathBox.Watermark = Watermark;
    }
}
