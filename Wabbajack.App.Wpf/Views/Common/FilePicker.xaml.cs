using FluentIcons.Common;
using System.Windows;

namespace Wabbajack;

/// <summary>
/// Interaction logic for FilePicker.xaml
/// </summary>
public partial class FilePicker
{
    // This exists, as utilizing the datacontext directly seemed to bug out the exit animations
    // "Bouncing" off this property seems to fix it, though.  Could perhaps be done other ways.
    public FilePickerVM PickerVM
    {
        get => (FilePickerVM)GetValue(PickerVMProperty);
        set => SetValue(PickerVMProperty, value);
    }
    public static readonly DependencyProperty PickerVMProperty = DependencyProperty.Register(nameof(PickerVM), typeof(FilePickerVM), typeof(FilePicker),
         new FrameworkPropertyMetadata(default(FilePickerVM)));

    public Symbol Icon
    {
        get => (Symbol)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(Symbol), typeof(FilePicker),
        new PropertyMetadata(default(Symbol)));
    public string Watermark
    {
        get => (string)GetValue(WatermarkProperty);
        set => SetValue(WatermarkProperty, value);
    }
    public static readonly DependencyProperty WatermarkProperty = DependencyProperty.Register(nameof(Watermark), typeof(string), typeof(FilePicker),
        new PropertyMetadata(default(string)));

    public FilePicker()
    {
        InitializeComponent();
    }
}
