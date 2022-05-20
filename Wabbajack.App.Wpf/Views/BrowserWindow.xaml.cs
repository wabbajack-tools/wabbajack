using System;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using MahApps.Metro.Controls;
using Wabbajack.Common;

namespace Wabbajack;

public partial class BrowserWindow : MetroWindow
{
    public BrowserWindow()
    {
        InitializeComponent();
    }

    private void UIElement_OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        base.DragMove();
    }

    private void BrowserWindow_OnActivated(object sender, EventArgs e)
    {
        var vm = ((BrowserWindowViewModel) DataContext);
        vm.Browser = this;
        vm.RunWrapper(CancellationToken.None)
            .ContinueWith(_ => Dispatcher.Invoke(Close));
    }
}