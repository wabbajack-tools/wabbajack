using ReactiveUI;
using Wabbajack.DTOs;
using Wabbajack.Paths;

namespace Wabbajack.Messages;

public class LoadModlistForInstalling
{
    public AbsolutePath Path { get; }
    
    public ModlistMetadata Metadata { get; }

    public LoadModlistForInstalling(AbsolutePath path, ModlistMetadata metadata)
    {
        Path = path;
        Metadata = metadata;
    }

    public static void Send(AbsolutePath path, ModlistMetadata metadata)
    {
        MessageBus.Current.SendMessage(new LoadModlistForInstalling(path, metadata));
    }
}