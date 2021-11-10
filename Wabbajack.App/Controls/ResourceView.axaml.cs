using System.Reactive.Disposables;
using Avalonia.ReactiveUI;
using FluentFTP.Helpers;
using ReactiveUI;

namespace Wabbajack.App.Controls;

public partial class ResourceView : ReactiveUserControl<ResourceViewModel>, IActivatableView
{
    public ResourceView()
    {
        InitializeComponent();
        this.WhenActivated(disposables =>
        {
            this.OneWayBind(ViewModel, vm => vm.Name, view => view.ResourceName.Text)
                .DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.MaxTasks, view => view.MaxTasks.Text)
                .DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.MaxThroughput, view => view.MaxThroughput.Text)
                .DisposeWith(disposables);

            this.OneWayBind(ViewModel, vm => vm.CurrentThroughput, view => view.CurrentThrougput.Text,
                    val => val.FileSizeToString())
                .DisposeWith(disposables);
        });
    }
}