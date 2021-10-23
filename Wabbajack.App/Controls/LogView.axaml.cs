using Avalonia.Controls;
using Avalonia.Controls.Mixins;
using Avalonia.ReactiveUI;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;

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
            this.BindCommand(ViewModel, vm => vm.CopyLogFile, view => view.CopyLog)
                .DisposeWith(disposables);
        });
    }

    private void ScrollViewer_OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        ScrollViewer.ScrollToEnd();
    }
}