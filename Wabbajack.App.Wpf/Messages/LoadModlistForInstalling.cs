using ReactiveUI;
using Wabbajack.Paths;

namespace Wabbajack.Messages;

public class LoadModlistForInstalling
{
    public AbsolutePath Path { get; }

    public LoadModlistForInstalling(AbsolutePath path)
    {
        Path = path;
    }

    public static void Send(AbsolutePath path)
    {
        MessageBus.Current.SendMessage(new LoadModlistForInstalling(path));
    }
}