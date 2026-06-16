using System.Windows.Input;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using ReactiveUI;
using Wabbajack.Messages;

namespace Wabbajack;

public partial class CompilerMainView : ReactiveUserControl<CompilerMainVM>
{
    public ICommand BackCommand { get; }

    public CompilerMainView()
    {
        BackCommand = ReactiveCommand.Create(() => NavigateToGlobal.Send(ScreenType.CompilerHome));
        AvaloniaXamlLoader.Load(this);
    }
}
