using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ReactiveUI;

namespace Wabbajack
{
    /// <summary>
    /// Interaction logic for CpuLineView.xaml
    /// </summary>
    public partial class CpuLineView : ReactiveUserControl<CPUDisplayVM>
    {
        public CpuLineView()
        {
            InitializeComponent();
            this.WhenActivated(dispose =>
            {
                this.WhenAny(x => x.ViewModel.ProgressPercent)
                    .Select(x => x.Value)
                    .BindToStrict(this, x => x.BackgroundProgressBar.Value)
                    .DisposeWith(dispose);
                this.WhenAny(x => x.ViewModel.ProgressPercent)
                    .Select(x => x.Value)
                    .BindToStrict(this, x => x.BackgroundProgressBar.Opacity)
                    .DisposeWith(dispose);
                this.WhenAny(x => x.ViewModel.ProgressPercent)
                    .Select(x => x.Value)
                    .BindToStrict(this, x => x.ThinProgressBar.Value)
                    .DisposeWith(dispose);

                this.WhenAny(x => x.ViewModel.Msg)
                    .BindToStrict(this, x => x.Text.Text)
                    .DisposeWith(dispose);
                this.WhenAny(x => x.ViewModel.Msg)
                    .BindToStrict(this, x => x.Text.ToolTip)
                    .DisposeWith(dispose);
            });
        }
    }
}
