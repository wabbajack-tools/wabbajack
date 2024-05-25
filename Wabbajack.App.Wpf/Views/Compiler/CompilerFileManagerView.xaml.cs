using System;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using ReactiveUI;
using System.Windows;
using System.Windows.Forms;
using DynamicData;
using Microsoft.WindowsAPICodePack.Dialogs;
using Wabbajack.Common;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.ViewModels.Controls;
using Wabbajack.Services.OSIntegrated;

namespace Wabbajack
{
    /// <summary>
    /// Interaction logic for CompilerFileManagerView.xaml
    /// </summary>
    public partial class CompilerFileManagerView : ReactiveUserControl<CompilerFileManagerVM>
    {
        public CompilerFileManagerView()
        {
            InitializeComponent();


            this.WhenActivated(disposables =>
            {
                this.WhenAny(x => x.ViewModel.Files)
                    .BindToStrict(this, v => v.FileTreeView.ItemsSource)
                    .DisposeWith(disposables);
            });

        }

    }
}
