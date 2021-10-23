using System;
using Newtonsoft.Json;

namespace Wabbajack.Server.DTOs;

public class DiscordMessage
{
    [JsonProperty("username")] public string UserName { get; set; }

    [JsonProperty("avatar_url")] public Uri AvatarUrl { get; set; }

    [JsonProperty("content")] public string Content { get; set; }

    [JsonProperty("embeds")] public DiscordEmbed[] Embeds { get; set; }
}

public class DiscordEmbed
{
    [JsonProperty("title")] public string Title { get; set; }

    [JsonProperty("color")] public int Color { get; set; }

    [JsonProperty("author")] public DiscordAuthor Author { get; set; }

    [JsonProperty("url")] public Uri Url { get; set; }

    [JsonProperty("description")] public string Description { get; set; }

    [JsonProperty("fields")] public DiscordField Field { get; set; }

    [JsonProperty("thumbnail")] public DiscordNumbnail Thumbnail { get; set; }

    [JsonProperty("image")] public DiscordImage Image { get; set; }

    [JsonProperty("footer")] public DiscordFooter Footer { get; set; }

    [JsonProperty("timestamp")] public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class DiscordAuthor
{
    [JsonProperty("name")] public string Name { get; set; }

    [JsonProperty("url")] public Uri Url { get; set; }

    [JsonProperty("icon_url")] public Uri IconUrl { get; set; }
}

public class DiscordField
{
    [JsonProperty("name")] public string Name { get; set; }

    [JsonProperty("value")] public string Value { get; set; }

    [JsonProperty("inline")] public bool Inline { get; set; }
}

public class DiscordNumbnail
{
    [JsonProperty("Url")] public Uri Url { get; set; }
}

public class DiscordImage
{
    [JsonProperty("Url")] public Uri Url { get; set; }
}

public class DiscordFooter
{
    [JsonProperty("text")] public string Text { get; set; }

    [JsonProperty("icon_url")] public Uri icon_url { get; set; }
}