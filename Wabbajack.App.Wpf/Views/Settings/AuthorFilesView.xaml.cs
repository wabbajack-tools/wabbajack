using System.Windows.Controls;
using ReactiveUI;
using Wabbajack.View_Models.Settings;

namespace Wabbajack
{
    public partial class AuthorFilesView : ReactiveUserControl<AuthorFilesVM>
    {
        public AuthorFilesView()
        {
            InitializeComponent();
        }
    }
}

