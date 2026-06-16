using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Wabbajack;

public partial class FilePicker : UserControl
{
    public static readonly StyledProperty<FilePickerVM?> PickerVMProperty =
        AvaloniaProperty.Register<FilePicker, FilePickerVM?>(nameof(PickerVM));

    public FilePickerVM? PickerVM { get => GetValue(PickerVMProperty); set => SetValue(PickerVMProperty, value); }

    public FilePicker() => InitializeComponent();
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
