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

    public FilePicker()
    {
        InitializeComponent();
    }
}
