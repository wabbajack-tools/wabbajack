using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;

namespace Wabbajack
{
    /// <summary>
    /// Interaction logic for CompilationCompleteView.xaml
    /// </summary>
    public partial class CompilationCompleteView
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
                    .Select(failed => $"Compilation {(failed ? "Failed" : "Complete")}")
                    .BindToStrict(this, x => x.TitleText.Text)
                    .DisposeWith(dispose);
                this.WhenAny(x => x.ViewModel.Completed)
                    .Select(x => x?.Failed ?? false)
                    .Select(failed => failed ? "Open Logs Folder" : "Go to Modlist")
                    .BindToStrict(this, x => x.ActionText.Text)
                    .DisposeWith(dispose);
                this.WhenAny(x => x.ViewModel.BackCommand)
                    .BindToStrict(this, x => x.BackButton.Command)
                    .DisposeWith(dispose);
                this.WhenAny(x => x.ViewModel.GoToCommand)
                    .BindToStrict(this, x => x.GoToModlistButton.Command)
                    .DisposeWith(dispose);
                this.WhenAny(x => x.ViewModel.CloseWhenCompleteCommand)
                    .BindToStrict(this, x => x.CloseWhenCompletedButton.Command)
                    .DisposeWith(dispose);
            });
        }
    }
}
