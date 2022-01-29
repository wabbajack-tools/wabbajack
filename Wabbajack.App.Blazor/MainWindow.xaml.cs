using System;
using System.Reactive.Disposables;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ReactiveUI;
using Wabbajack.App.Blazor.Browser;
using Wabbajack.App.Blazor.Messages;
using Wabbajack.App.Blazor.State;

namespace Wabbajack.App.Blazor;

public partial class MainWindow : IDisposable
{
    private Point _lastPosition;
    private readonly CompositeDisposable _compositeDisposable;

    public MainWindow(IServiceProvider serviceProvider, IStateContainer stateContainer)
    {
        _compositeDisposable = new CompositeDisposable();
        
        stateContainer.TaskBarStateObservable.Subscribe(state =>
        {
            Dispatcher.InvokeAsync(() =>
            {
                TaskBarItem.Description = state.Description;
                TaskBarItem.ProgressState = state.State;
                TaskBarItem.ProgressValue = state.ProgressValue;
            });
        });

        MessageBus.Current.Listen<OpenBrowserTab>()
            .Subscribe(OnOpenBrowserTab)
            .DisposeWith(_compositeDisposable);
        
        InitializeComponent();
        BlazorWebView.Services = serviceProvider;
    }

    private void UIElement_OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        this.DragMove();
    }

    private void OnOpenBrowserTab(OpenBrowserTab msg)
    {
        var tab = new BrowserTabView(msg.ViewModel);
        Tabs.Items.Add(tab);
        Tabs.SelectedItem = tab;
    }

    public void Dispose()
    {
        _compositeDisposable.Dispose();
    }
}

// Required so compiler doesn't complain about not finding the type. [MC3050]
public partial class Main { }
