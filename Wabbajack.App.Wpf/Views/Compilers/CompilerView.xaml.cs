using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Controls;
using ReactiveUI;
using System.Windows;
using DynamicData;
using Wabbajack.Common;

namespace Wabbajack
{
    /// <summary>
    /// Interaction logic for CompilerView.xaml
    /// </summary>
    public partial class CompilerView : ReactiveUserControl<CompilerVM>
    {
        public CompilerView()
        {
            InitializeComponent();

        }
    }
}
