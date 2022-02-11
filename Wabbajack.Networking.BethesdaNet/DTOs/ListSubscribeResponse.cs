using System.Text.Json.Serialization;

namespace Wabbajack.Networking.BethesdaNet.DTOs;

public class Price
{
    [JsonPropertyName("currency_id")]
    public int CurrencyId { get; set; }

    [JsonPropertyName("price_id")]
    public int PriceId { get; set; }
    
    [JsonPropertyName("sale")]
    public bool Sale { get; set; }

    [JsonPropertyName("currency_name")]
    public string CurrencyName { get; set; }

    [JsonPropertyName("currency_type")]
    public string CurrencyType { get; set; }

    [JsonPropertyName("amount")]
    public int Amount { get; set; }

    [JsonPropertyName("original_amount")]
    public int OriginalAmount { get; set; }
}

public class Content
{
    [JsonPropertyName("rating")]
    public double Rating { get; set; }

    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("depot_size")]
    public int DepotSize { get; set; }

    [JsonPropertyName("is_subscribed")]
    public bool IsSubscribed { get; set; }

    [JsonPropertyName("preview_file_size")]
    public int PreviewFileSize { get; set; }

    [JsonPropertyName("preview_file_url")]
    public string PreviewFileUrl { get; set; }

    [JsonPropertyName("is_following")]
    public bool IsFollowing { get; set; }

    [JsonPropertyName("wip")]
    public bool Wip { get; set; }

    [JsonPropertyName("is_published")]
    public bool IsPublished { get; set; }

    [JsonPropertyName("user_rating")]
    public int UserRating { get; set; }

    [JsonPropertyName("platform")]
    public List<string> Platform { get; set; }

    [JsonPropertyName("state")]
    public int State { get; set; }

    [JsonPropertyName("rating_count")]
    public int RatingCount { get; set; }

    [JsonPropertyName("cdp_branch_id")]
    public long CdpBranchId { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("username")]
    public string Username { get; set; }

    [JsonPropertyName("product")]
    public string Product { get; set; }

    [JsonPropertyName("updated")]
    public string Updated { get; set; }

    [JsonPropertyName("cc_mod")]
    public bool CcMod { get; set; }

    [JsonPropertyName("bundle")]
    public bool Bundle { get; set; }

    [JsonPropertyName("prices")]
    public List<Price> Prices { get; set; }

    [JsonPropertyName("is_public")]
    public bool IsPublic { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("catalog_item_id")]
    public int CatalogItemId { get; set; }

    [JsonPropertyName("is_auto_moderated")]
    public bool IsAutoModerated { get; set; }

    [JsonPropertyName("content_id")]
    public string ContentId { get; set; }

    [JsonPropertyName("cdp_product_id")]
    public long CdpProductId { get; set; }
}

public class Response
{
    [JsonPropertyName("product")]
    public List<string> Product { get; set; }

    [JsonPropertyName("total_results_count")]
    public int TotalResultsCount { get; set; }

    [JsonPropertyName("content")]
    public List<Content> Content { get; set; }

    [JsonPropertyName("platform")]
    public List<string> Platform { get; set; }

    [JsonPropertyName("page_results_count")]
    public int PageResultsCount { get; set; }

    [JsonPropertyName("page")]
    public int Page { get; set; }
}

public class Platform
{
    [JsonPropertyName("message")]
    public string Message { get; set; }

    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("response")]
    public Response Response { get; set; }
}

public class ListSubscribeResponse
{
    [JsonPropertyName("platform")]
    public Platform Platform { get; set; }
}

