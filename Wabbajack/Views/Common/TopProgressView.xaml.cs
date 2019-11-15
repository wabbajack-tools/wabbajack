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
    /// Interaction logic for TopProgressView.xaml
    /// </summary>
    public partial class TopProgressView : UserControl
    {
        public double ProgressPercent
        {
            get => (double)GetValue(ProgressPercentProperty);
            set => SetValue(ProgressPercentProperty, value);
        }
        public static readonly DependencyProperty ProgressPercentProperty = DependencyProperty.Register(nameof(ProgressPercent), typeof(double), typeof(TopProgressView),
             new FrameworkPropertyMetadata(default(double)));

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }
        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(TopProgressView),
             new FrameworkPropertyMetadata(default(string)));

        public string StatePrefixTitle
        {
            get => (string)GetValue(StatePrefixTitleProperty);
            set => SetValue(StatePrefixTitleProperty, value);
        }
        public static readonly DependencyProperty StatePrefixTitleProperty = DependencyProperty.Register(nameof(StatePrefixTitle), typeof(string), typeof(TopProgressView),
             new FrameworkPropertyMetadata(default(string)));

        public bool OverhangShadow
        {
            get => (bool)GetValue(OverhangShadowProperty);
            set => SetValue(OverhangShadowProperty, value);
        }
        public static readonly DependencyProperty OverhangShadowProperty = DependencyProperty.Register(nameof(OverhangShadow), typeof(bool), typeof(TopProgressView),
             new FrameworkPropertyMetadata(true));

        public TopProgressView()
        {
            InitializeComponent();
        }
    }
}
