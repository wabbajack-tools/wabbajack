using Avalonia.ReactiveUI;
using Wabbajack.ViewModels;

namespace Wabbajack.Views;

public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
{
    public MainWindow()
    {
        InitializeComponent();
    }
}
