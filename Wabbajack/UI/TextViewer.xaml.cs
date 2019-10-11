using System;
using System.Windows;
using System.Windows.Documents;

namespace Wabbajack.UI
{
    public partial class TextViewer : Window
    {
        public TextViewer(string text)
        {
            InitializeComponent();
            TextBlock.Text = text;
            
        }
    }
}
