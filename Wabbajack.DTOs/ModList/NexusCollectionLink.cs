using System.Text.Json.Serialization;

namespace Wabbajack.DTOs;

public sealed class NexusCollectionLink
{
    [JsonPropertyName("collectionId")]
    public int CollectionId { get; set; }

    [JsonPropertyName("slug")]
    public string Slug { get; set; } = string.Empty;

    // "skyrimspecialedition" etc
    [JsonPropertyName("domainName")]
    public string DomainName { get; set; } = string.Empty;

    [JsonPropertyName("lastRevisionNumber")]
    public int? LastRevisionNumber { get; set; }
}