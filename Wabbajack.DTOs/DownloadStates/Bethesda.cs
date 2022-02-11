using Wabbajack.DTOs.JsonConverters;

namespace Wabbajack.DTOs.DownloadStates;

[JsonName("Bethesda")]
public class Bethesda : ADownloadState
{
    public override string TypeName { get; } = "Bethesda";
    public override object[] PrimaryKey => new object[] {Game, IsCCMod, ProductId, BranchId, ContentId};
    public Game Game { get; set; }
    public bool IsCCMod { get; set; }
    public string ContentId { get; set; }
    public long ProductId { get; set; }
    public long BranchId { get; set; }
    public string Name { get; set; }
}