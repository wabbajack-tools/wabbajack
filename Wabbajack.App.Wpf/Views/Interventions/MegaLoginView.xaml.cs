using System;
using ReactiveUI;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveMarbles.ObservableEvents;
using System.Windows.Input;
using System.Windows;
using System.IO;
using Wabbajack.Paths;

namespace Wabbajack;

public partial class MegaLoginView : ReactiveUserControl<MegaLoginVM>
{
    public MegaLoginView()
    {
        InitializeComponent();

        this.WhenActivated(disposables =>
        {
            this.BindCommand(ViewModel, vm => vm.CloseCommand, v => v.CloseButton)
                .DisposeWith(disposables);
        });
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        var password = PasswordBox.Password;
        ViewModel.Password = password;
    }
}

