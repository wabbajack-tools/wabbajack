using System;
using System.Text.Json.Serialization;

namespace Wabbajack.Networking.Discord;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Channel
{
    Spam,
    Ham
}

public class DiscordMessage
{
    [JsonPropertyName("username")] public string UserName { get; set; }

    [JsonPropertyName("avatar_url")] public Uri AvatarUrl { get; set; }

    [JsonPropertyName("content")] public string Content { get; set; }

    [JsonPropertyName("embeds")] public DiscordEmbed[] Embeds { get; set; }
}

public class DiscordEmbed
{
    [JsonPropertyName("title")] public string Title { get; set; }

    [JsonPropertyName("color")] public int Color { get; set; }

    [JsonPropertyName("author")] public DiscordAuthor Author { get; set; }

    [JsonPropertyName("url")] public Uri Url { get; set; }

    [JsonPropertyName("description")] public string Description { get; set; }

    [JsonPropertyName("fields")] public DiscordField Field { get; set; }

    [JsonPropertyName("thumbnail")] public DiscordNumbnail Thumbnail { get; set; }

    [JsonPropertyName("image")] public DiscordImage Image { get; set; }

    [JsonPropertyName("footer")] public DiscordFooter Footer { get; set; }

    [JsonPropertyName("timestamp")] public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class DiscordAuthor
{
    [JsonPropertyName("name")] public string Name { get; set; }

    [JsonPropertyName("url")] public Uri Url { get; set; }

    [JsonPropertyName("icon_url")] public Uri IconUrl { get; set; }
}

public class DiscordField
{
    [JsonPropertyName("name")] public string Name { get; set; }

    [JsonPropertyName("value")] public string Value { get; set; }

    [JsonPropertyName("inline")] public bool Inline { get; set; }
}

public class DiscordNumbnail
{
    [JsonPropertyName("Url")] public Uri Url { get; set; }
}

public class DiscordImage
{
    [JsonPropertyName("Url")] public Uri Url { get; set; }
}

public class DiscordFooter
{
    [JsonPropertyName("text")] public string Text { get; set; }

    [JsonPropertyName("icon_url")] public Uri icon_url { get; set; }
}