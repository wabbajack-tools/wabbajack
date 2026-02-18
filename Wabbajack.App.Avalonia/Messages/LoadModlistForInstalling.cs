using ReactiveUI;
using Wabbajack.DTOs;
using Wabbajack.Paths;

namespace Wabbajack.App.Avalonia.Messages;

public class LoadModlistForInstalling
{
    public AbsolutePath Path { get; }
    public ModlistMetadata Metadata { get; }

    private LoadModlistForInstalling(AbsolutePath path, ModlistMetadata metadata)
    {
        Path = path;
        Metadata = metadata;
    }

    public static void Send(AbsolutePath path, ModlistMetadata metadata)
        => MessageBus.Current.SendMessage(new LoadModlistForInstalling(path, metadata));
}
