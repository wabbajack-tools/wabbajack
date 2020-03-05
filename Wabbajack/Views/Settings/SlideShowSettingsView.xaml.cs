using ReactiveUI;

namespace Wabbajack
{
    /// <summary>
    /// Interaction logic for SlideShowSettingsView.xaml
    /// </summary>
    public partial class SlideShowSettingsView
    {
        public SlideShowSettingsView()
        {
            InitializeComponent();

            this.WhenActivated(disposable =>
            {
                AllowNSFWCheckbox.IsChecked = ViewModel.AllowNSFW;
                OnlyNSFWCheckbox.IsChecked = ViewModel.OnlyNSFW;
            });

            AllowNSFWCheckbox.Command =
                ReactiveCommand.Create(() => ViewModel.AllowNSFW = AllowNSFWCheckbox.IsChecked ?? false);
            OnlyNSFWCheckbox.Command =
                ReactiveCommand.Create(() => ViewModel.OnlyNSFW = OnlyNSFWCheckbox.IsChecked ?? false);
        }
    }
}
