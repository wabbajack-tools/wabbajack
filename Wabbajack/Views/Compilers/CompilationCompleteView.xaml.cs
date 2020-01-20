using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
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
    /// Interaction logic for CompilationCompleteView.xaml
    /// </summary>
    public partial class CompilationCompleteView : ReactiveUserControl<CompilerVM>
    {
        public CompilationCompleteView()
        {
            InitializeComponent();
            this.WhenActivated(dispose =>
            {
                this.WhenAny(x => x.ViewModel.Completed)
                    .Select(x => x?.Failed ?? false)
                    .BindToStrict(this, x => x.AttentionBorder.Failure)
                    .DisposeWith(dispose);
                this.WhenAny(x => x.ViewModel.Completed)
                    .Select(x => x?.Failed ?? false)
                    .Select(failed =>
                    {
                        return $"Compilation {(failed ? "Failed" : "Complete")}";
                    })
                    .BindToStrict(this, x => x.TitleText.Text)
                    .DisposeWith(dispose);
                this.WhenAny(x => x.ViewModel.BackCommand)
                    .BindToStrict(this, x => x.BackButton.Command)
                    .DisposeWith(dispose);
                this.WhenAny(x => x.ViewModel.GoToModlistCommand)
                    .BindToStrict(this, x => x.GoToModlistButton.Command)
                    .DisposeWith(dispose);
                this.WhenAny(x => x.ViewModel.CloseWhenCompleteCommand)
                    .BindToStrict(this, x => x.CloseWhenCompletedButton.Command)
                    .DisposeWith(dispose);
            });
        }
    }
}
