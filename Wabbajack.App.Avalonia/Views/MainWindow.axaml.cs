using Avalonia.ReactiveUI;
using Wabbajack.App.Avalonia.ViewModels;

namespace Wabbajack.App.Avalonia.Views;

public partial class MainWindow : ReactiveWindow<MainWindowVM>
{
    public MainWindow()
    {
        InitializeComponent();
    }
}
