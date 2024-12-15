using System;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Windows.Controls;
using System.Windows;
using ReactiveUI;
using System.Threading;
using ReactiveMarbles.ObservableEvents;

namespace Wabbajack;

public partial class BrowserWindow : ReactiveUserControl<BrowserWindowViewModel>, IActivatableViewModel
{
    public ViewModelActivator Activator { get; }
    public BrowserWindow()
    {
        InitializeComponent();
        Activator = new ViewModelActivator();

        this.WhenActivated(disposables =>
        {
            this.BindCommand(ViewModel, vm => vm.BackCommand, v => v.BackButton)
                .DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.CloseCommand, v => v.CloseButton)
                .DisposeWith(disposables);


            this.WhenAnyValue(v => v.ViewModel.HeaderText)
                .BindToStrict(this, view => view.Header.Text)
                .DisposeWith(disposables);

            this.WhenAnyValue(v => v.ViewModel.Instructions)
                .BindToStrict(this, view => view.Instructions.Text)
                .DisposeWith(disposables);

            this.WhenAnyValue(v => v.ViewModel.Address)
                .BindToStrict(this, view => view.AddressBar.Text)
                .DisposeWith(disposables);

            this.WhenAnyValue(x => x.ViewModel.Browser)
                .WhereNotNull()
                .ObserveOnGuiThread()
                .Subscribe(browser =>
                {
                    RxApp.MainThreadScheduler.Schedule(() =>
                    {
                        if (browser.Parent != null)
                        {
                            ((Panel)browser.Parent).Children.Remove(browser);
                        }
                        ViewModel.Browser.Visibility = Visibility.Visible;
                        WebViewGrid.Children.Add(browser);
                    });
                })
                .DisposeWith(disposables);
            /*
            RxApp.MainThreadScheduler.Schedule(() =>
            {
                var browser = ViewModel.Browser;
                if (browser.Parent != null)
                {
                    ((Panel)browser.Parent).Children.Remove(browser);
                }
                ViewModel.Browser.Visibility = Visibility.Visible;
                WebViewGrid.Children.Add(browser);
            });
            */
        });
    }
}