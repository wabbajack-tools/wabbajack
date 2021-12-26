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

namespace Wabbajack
{
    /// <summary>
    /// Interaction logic for AttentionBorder.xaml
    /// </summary>
    public partial class AttentionBorder : UserControl
    {
        public bool Failure
        {
            get => (bool)GetValue(FailureProperty);
            set => SetValue(FailureProperty, value);
        }
        public static readonly DependencyProperty FailureProperty = DependencyProperty.Register(nameof(Failure), typeof(bool), typeof(AttentionBorder),
             new FrameworkPropertyMetadata(default(bool)));
    }
}
