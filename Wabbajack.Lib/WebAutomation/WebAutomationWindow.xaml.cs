using System.Windows;
using Wabbajack.Lib.WebAutomation;

namespace Wabbajack
{
    /// <summary>
    /// Interaction logic for WebAutomationWindow.xaml
    /// </summary>
    public partial class WebAutomationWindow : Window
    {
        public WebAutomationWindow()
        {
            InitializeComponent();
            DataContext = new WebAutomationWindowViewModel(this);
        }
    }
}
