using System.Windows;

namespace Wabbajack.UI
{
    public partial class TextViewer : Window
    {
        public TextViewer(string text, string title)
        {
            InitializeComponent();
            TextBlock.Text = text;
            Title = title;
        }
    }
}
