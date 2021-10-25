namespace Wabbajack.DTOs.ServerResponses;

public class ModUpgradeRequest
{
    public Archive OldArchive { get; set; }
    public Archive NewArchive { get; set; }
}