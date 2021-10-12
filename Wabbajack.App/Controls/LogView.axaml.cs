using Avalonia.Controls.Mixins;
using Avalonia.ReactiveUI;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using Wabbajack.App.Utilities;

namespace Wabbajack.App.Controls;

public partial class LogView : ReactiveUserControl<LogViewModel>
{
    public LogView()
    {
        DataContext = App.Services.GetService<LogViewModel>()!;
        InitializeComponent();
        this.WhenActivated(disposables =>
        {
            this.OneWayBind(ViewModel, vm => vm.Messages, view => view.Messages.Items)
                .DisposeWith(disposables);
        });
    }
    
}