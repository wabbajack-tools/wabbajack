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
using ReactiveMarbles.ObservableEvents;
using System.Reactive;

namespace Wabbajack
{
    /// <summary>
    /// Interaction logic for CreateModList.xaml
    /// </summary>
    public partial class CompilerHomeView : ReactiveUserControl<CompilerHomeVM>
    {
        public CompilerHomeView()
        {
            InitializeComponent();

            this.WhenActivated(dispose =>
            {
                this.WhenAny(x => x.ViewModel.CompiledModLists)
                    .BindToStrict(this, x => x.CompiledModListsControl.ItemsSource)
                    .DisposeWith(dispose);

                NewModListBorder
                .Events().MouseDown
                .Select(args => Unit.Default)
                .InvokeCommand(this, x => x.ViewModel.NewModListCommand)
                .DisposeWith(dispose);

                LoadSettingsBorder
                .Events().MouseDown
                .Select(args => Unit.Default)
                .InvokeCommand(this, x => x.ViewModel.LoadSettingsCommand)
                .DisposeWith(dispose);
            });
        }
    }
}
