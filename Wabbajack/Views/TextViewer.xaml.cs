using System.Windows;

namespace Wabbajack
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
