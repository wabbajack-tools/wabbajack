using Avalonia.Controls.Mixins;
using Avalonia.ReactiveUI;
using ReactiveUI;
using Wabbajack.App.Utilities;

namespace Wabbajack.App.Controls;

public partial class LogViewItem : ReactiveUserControl<LoggerProvider.ILogMessage>, IActivatableView
{
    public LogViewItem()
    {
        InitializeComponent();
        this.WhenActivated(disposables =>
        {
            this.OneWayBind(ViewModel, vm => vm.ShortMessage, view => view.Message.Text)
                .DisposeWith(disposables);
        });
    }
    
}