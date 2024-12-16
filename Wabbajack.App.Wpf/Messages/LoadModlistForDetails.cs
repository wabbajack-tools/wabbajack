using ReactiveUI;
using Wabbajack.DTOs;

namespace Wabbajack.Messages;

public class LoadModlistForDetails
{
    public BaseModListMetadataVM MetadataVM { get; }

    public LoadModlistForDetails(BaseModListMetadataVM metadata)
    {
        MetadataVM = metadata;
    }

    public static void Send(BaseModListMetadataVM metadataVM)
    {
        MessageBus.Current.SendMessage(new LoadModlistForDetails(metadataVM));
    }
}