using ReactiveUI;
using Wabbajack.DTOs;

namespace Wabbajack.Messages;

public class ReloadCompiledModLists
{
    public ReloadCompiledModLists()
    {
    }

    public static void Send()
    {
        MessageBus.Current.SendMessage(new ReloadCompiledModLists());
    }
}