namespace Wabbajack.DTOs.ServerResponses;

public class DetailedStatusItem
{
    public bool IsFailing { get; set; }
    public Archive Archive { get; set; }

    public string Name => string.IsNullOrWhiteSpace(Archive.Name) ? Archive.State.PrimaryKeyString : Archive.Name;

    public ArchiveStatus ArchiveStatus { get; set; }
}