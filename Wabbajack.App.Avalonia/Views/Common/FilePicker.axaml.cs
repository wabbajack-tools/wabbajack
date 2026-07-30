using Avalonia;
using Avalonia.Controls;
using FluentIcons.Common;

namespace Wabbajack;

/// <summary>
/// Interaction logic for FilePicker.axaml
/// </summary>
public partial class FilePicker : UserControl
{
    // This exists, as utilizing the datacontext directly seemed to bug out the exit animations
    // "Bouncing" off this property seems to fix it, though.  Could perhaps be done other ways.
    public static readonly StyledProperty<FilePickerVM> PickerVMProperty =
        AvaloniaProperty.Register<FilePicker, FilePickerVM>(nameof(PickerVM));

    public FilePickerVM PickerVM
    {
        get => GetValue(PickerVMProperty);
        set => SetValue(PickerVMProperty, value);
    }

    public static readonly StyledProperty<Symbol> IconProperty =
        AvaloniaProperty.Register<FilePicker, Symbol>(nameof(Icon));

    public Symbol Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public static readonly StyledProperty<string> WatermarkProperty =
        AvaloniaProperty.Register<FilePicker, string>(nameof(Watermark));

    public string Watermark
    {
        get => GetValue(WatermarkProperty);
        set => SetValue(WatermarkProperty, value);
    }

    public FilePicker()
    {
        InitializeComponent();
    }
}
