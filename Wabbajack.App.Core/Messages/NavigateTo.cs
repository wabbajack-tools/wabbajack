using ReactiveUI;

namespace Wabbajack.Messages;

public class NavigateTo
{

    public ViewModel ViewModel { get; }
    private NavigateTo(ViewModel vm)
    {
        ViewModel = vm;
    }

    public static void Send<T>(T vm)
        where T : ViewModel
    {
        MessageBus.Current.SendMessage(new NavigateTo(vm));
    }

}