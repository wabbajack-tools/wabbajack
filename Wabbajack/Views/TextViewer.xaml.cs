namespace Wabbajack
{
    public partial class TextViewer
    {
        public TextViewer(string text, string title)
        {
            InitializeComponent();
            TextBlock.Text = text;
            Title = title;
        }
    }
}
