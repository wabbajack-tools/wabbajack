using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Wabbajack.Downloaders.IPS4OAuth2Downloader;

public class IPS4OAuthFilesResponse
{
// Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse); 
    public class Category
    {
        [JsonPropertyName("id")] public int Id { get; set; }

        [JsonPropertyName("name")] public string? Name { get; set; }

        [JsonPropertyName("url")] public string? Url { get; set; }

        [JsonPropertyName("class")] public string? Class { get; set; }
    }

    public class PrimaryGroup
    {
        [JsonPropertyName("id")] public int Id { get; set; }

        [JsonPropertyName("name")] public string? Name { get; set; }

        [JsonPropertyName("formattedName")] public string? FormattedName { get; set; }
    }

    public class Author
    {
        [JsonPropertyName("id")] public int? Id { get; set; }

        [JsonPropertyName("name")] public string? Name { get; set; }

        [JsonPropertyName("title")] public string? Title { get; set; }

        [JsonPropertyName("formattedName")] public string? FormattedName { get; set; }

        [JsonPropertyName("primaryGroup")] public PrimaryGroup? PrimaryGroup { get; set; }

        [JsonPropertyName("joined")] public DateTime Joined { get; set; }

        [JsonPropertyName("reputationPoints")] public int ReputationPoints { get; set; }

        [JsonPropertyName("photoUrl")] public string? PhotoUrl { get; set; }

        [JsonPropertyName("photoUrlIsDefault")]
        public bool PhotoUrlIsDefault { get; set; }

        [JsonPropertyName("coverPhotoUrl")] public string? CoverPhotoUrl { get; set; }

        [JsonPropertyName("profileUrl")] public string? ProfileUrl { get; set; }

        [JsonPropertyName("posts")] public int Posts { get; set; }

        [JsonPropertyName("lastActivity")] public DateTime? LastActivity { get; set; }

        [JsonPropertyName("lastVisit")] public DateTime? LastVisit { get; set; }

        [JsonPropertyName("lastPost")] public DateTime? LastPost { get; set; }

        [JsonPropertyName("profileViews")] public int? ProfileViews { get; set; }
    }

    public class File
    {
        [JsonPropertyName("name")] public string? Name { get; set; }

        [JsonPropertyName("url")] public string? Url { get; set; }

        [JsonPropertyName("size")] public long? Size { get; set; }
    }

    public class Screenshot
    {
        [JsonPropertyName("name")] public string? Name { get; set; }

        [JsonPropertyName("url")] public string? Url { get; set; }
    }

    public class PrimaryScreenshot
    {
        [JsonPropertyName("name")] public string? Name { get; set; }

        [JsonPropertyName("url")] public string? Url { get; set; }
    }

    public class Forum
    {
        [JsonPropertyName("id")] public int Id { get; set; }

        [JsonPropertyName("name")] public string? Name { get; set; }

        [JsonPropertyName("topics")] public int Topics { get; set; }

        [JsonPropertyName("url")] public string? Url { get; set; }
    }

    public class FirstPost
    {
        [JsonPropertyName("id")] public int Id { get; set; }

        [JsonPropertyName("item_id")] public int ItemId { get; set; }

        [JsonPropertyName("author")] public Author? Author { get; set; }

        [JsonPropertyName("date")] public DateTime Date { get; set; }

        [JsonPropertyName("content")] public string? Content { get; set; }

        [JsonPropertyName("hidden")] public bool Hidden { get; set; }

        [JsonPropertyName("url")] public string? Url { get; set; }
    }

    public class LastPost
    {
        [JsonPropertyName("id")] public int Id { get; set; }

        [JsonPropertyName("item_id")] public int ItemId { get; set; }

        [JsonPropertyName("author")] public Author? Author { get; set; }

        [JsonPropertyName("date")] public DateTime Date { get; set; }

        [JsonPropertyName("content")] public string? Content { get; set; }

        [JsonPropertyName("hidden")] public bool Hidden { get; set; }

        [JsonPropertyName("url")] public string? Url { get; set; }
    }

    public class Topic
    {
        [JsonPropertyName("id")] public int Id { get; set; }

        [JsonPropertyName("title")] public string? Title { get; set; }

        [JsonPropertyName("forum")] public Forum? Forum { get; set; }

        [JsonPropertyName("posts")] public int Posts { get; set; }

        [JsonPropertyName("views")] public int Views { get; set; }

        [JsonPropertyName("locked")] public bool Locked { get; set; }

        [JsonPropertyName("hidden")] public bool Hidden { get; set; }

        [JsonPropertyName("pinned")] public bool Pinned { get; set; }

        [JsonPropertyName("featured")] public bool Featured { get; set; }

        [JsonPropertyName("archived")] public bool Archived { get; set; }

        [JsonPropertyName("url")] public string? Url { get; set; }

        [JsonPropertyName("rating")] public double Rating { get; set; }
    }

    public class Root
    {
        [JsonPropertyName("id")] public int Id { get; set; }

        [JsonPropertyName("title")] public string? Title { get; set; }

        [JsonPropertyName("category")] public Category? Category { get; set; }

        [JsonPropertyName("author")] public Author? Author { get; set; }

        [JsonPropertyName("date")] public DateTime Date { get; set; }

        [JsonPropertyName("description")] public string? Description { get; set; }

        [JsonPropertyName("version")] public string? Version { get; set; }

        [JsonPropertyName("changelog")] public string? Changelog { get; set; }

        [JsonPropertyName("files")] public List<File> Files { get; set; } = new();

        [JsonPropertyName("screenshots")] public List<Screenshot> Screenshots { get; set; } = new();

        [JsonPropertyName("primaryScreenshot")]
        public PrimaryScreenshot PrimaryScreenshot { get; set; } = new();

        [JsonPropertyName("downloads")] public int Downloads { get; set; }

        [JsonPropertyName("comments")] public int Comments { get; set; }

        [JsonPropertyName("reviews")] public int Reviews { get; set; }

        [JsonPropertyName("views")] public int Views { get; set; }

        [JsonPropertyName("tags")] public List<string?> Tags { get; set; } = new();

        [JsonPropertyName("locked")] public bool Locked { get; set; }

        [JsonPropertyName("hidden")] public bool Hidden { get; set; }

        [JsonPropertyName("pinned")] public bool Pinned { get; set; }

        [JsonPropertyName("featured")] public bool Featured { get; set; }

        [JsonPropertyName("url")] public string? Url { get; set; }

        [JsonPropertyName("topic")] public Topic Topic { get; set; } = new();
    }
}