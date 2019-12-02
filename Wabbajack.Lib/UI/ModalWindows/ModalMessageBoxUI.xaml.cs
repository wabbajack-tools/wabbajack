using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Wabbajack.Lib.UI.ModalWindows
{
    public partial class ModalMessageBoxUI : UserControl
    {
        public ModalMessageBoxUI()
        {
            InitializeComponent();
        }

        private void OkClick(object sender, RoutedEventArgs e)
        {
            AModalWindowFactory.CurrentFactory.SetResult(ModalMessageBox.Result.Ok);
        }

        private void OkCancel(object sender, RoutedEventArgs e)
        {
            AModalWindowFactory.CurrentFactory.SetResult(ModalMessageBox.Result.Cancel);
        }
    }
}
