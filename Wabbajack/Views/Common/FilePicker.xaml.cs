using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Wabbajack.Lib;
using Wabbajack.UI;

namespace Wabbajack
{
    /// <summary>
    /// Interaction logic for FilePicker.xaml
    /// </summary>
    public partial class FilePicker : UserControl
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
}
