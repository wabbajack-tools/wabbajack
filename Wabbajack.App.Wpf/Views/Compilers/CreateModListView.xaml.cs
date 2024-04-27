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

namespace Wabbajack
{
    /// <summary>
    /// Interaction logic for CreateAListView.xaml
    /// </summary>
    public partial class CreateModListView : ReactiveUserControl<CreateModListVM>
    {
        public CreateModListView()
        {
            InitializeComponent();

            this.WhenActivated(dispose =>
            {
                this.WhenAny(x => x.ViewModel.ModLists)
                    .BindToStrict(this, x => x.CreatedModListsControl.ItemsSource)
                    .DisposeWith(dispose);
            });

        }
    }
}
