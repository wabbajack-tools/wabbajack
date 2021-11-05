using Wabbajack.App.Views;

namespace Wabbajack.App.Screens;

public partial class LogScreenView : ScreenBase<LogScreenViewModel>
{
    public LogScreenView() : base("Application Log")
    {
        InitializeComponent();
    }
}