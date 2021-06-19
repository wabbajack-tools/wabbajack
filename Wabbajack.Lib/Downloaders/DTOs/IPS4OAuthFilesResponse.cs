using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Wabbajack.Lib.Downloaders.DTOs
{
    public class IPS4OAuthFilesResponse
    {
        
// Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse); 
    public class Category
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("url")]
        public string? Url { get; set; }

        [JsonProperty("class")]
        public string? Class { get; set; }
    }

    public class PrimaryGroup
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("formattedName")]
        public string? FormattedName { get; set; }
    }

    public class Author
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("title")]
        public string? Title { get; set; }

        [JsonProperty("formattedName")]
        public string? FormattedName { get; set; }

        [JsonProperty("primaryGroup")]
        public PrimaryGroup? PrimaryGroup { get; set; }

        [JsonProperty("joined")]
        public DateTime Joined { get; set; }

        [JsonProperty("reputationPoints")]
        public int ReputationPoints { get; set; }

        [JsonProperty("photoUrl")]
        public string? PhotoUrl { get; set; }

        [JsonProperty("photoUrlIsDefault")]
        public bool PhotoUrlIsDefault { get; set; }

        [JsonProperty("coverPhotoUrl")]
        public string? CoverPhotoUrl { get; set; }

        [JsonProperty("profileUrl")]
        public string? ProfileUrl { get; set; }

        [JsonProperty("posts")]
        public int Posts { get; set; }

        [JsonProperty("lastActivity")]
        public DateTime? LastActivity { get; set; }

        [JsonProperty("lastVisit")]
        public DateTime? LastVisit { get; set; }

        [JsonProperty("lastPost")]
        public DateTime? LastPost { get; set; }

        [JsonProperty("profileViews")]
        public int ProfileViews { get; set; }

    }

    public class File
    {
        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("url")]
        public string? Url { get; set; }

        [JsonProperty("size")]
        public long? Size { get; set; }
    }

    public class Screenshot
    {
        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("url")]
        public string? Url { get; set; }

        [JsonProperty("size")]
        public int Size { get; set; }
    }

    public class PrimaryScreenshot
    {
        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("url")]
        public string? Url { get; set; }

        [JsonProperty("size")]
        public string? Size { get; set; }
    }

    public class Forum
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("topics")]
        public int Topics { get; set; }

        [JsonProperty("url")]
        public string? Url { get; set; }
    }

    public class FirstPost
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("item_id")]
        public int ItemId { get; set; }

        [JsonProperty("author")]
        public Author? Author { get; set; }

        [JsonProperty("date")]
        public DateTime Date { get; set; }

        [JsonProperty("content")]
        public string? Content { get; set; }

        [JsonProperty("hidden")]
        public bool Hidden { get; set; }

        [JsonProperty("url")]
        public string? Url { get; set; }
    }

    public class LastPost
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("item_id")]
        public int ItemId { get; set; }

        [JsonProperty("author")]
        public Author? Author { get; set; }

        [JsonProperty("date")]
        public DateTime Date { get; set; }

        [JsonProperty("content")]
        public string? Content { get; set; }

        [JsonProperty("hidden")]
        public bool Hidden { get; set; }

        [JsonProperty("url")]
        public string? Url { get; set; }
    }

    public class Topic
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("title")]
        public string? Title { get; set; }

        [JsonProperty("forum")]
        public Forum? Forum { get; set; }

        [JsonProperty("posts")]
        public int Posts { get; set; }

        [JsonProperty("views")]
        public int Views { get; set; }
        
        [JsonProperty("locked")]
        public bool Locked { get; set; }

        [JsonProperty("hidden")]
        public bool Hidden { get; set; }

        [JsonProperty("pinned")]
        public bool Pinned { get; set; }

        [JsonProperty("featured")]
        public bool Featured { get; set; }

        [JsonProperty("archived")]
        public bool Archived { get; set; }

        [JsonProperty("url")]
        public string? Url { get; set; }

        [JsonProperty("rating")]
        public double Rating { get; set; }
    }

    public class Root
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("title")]
        public string? Title { get; set; }

        [JsonProperty("category")]
        public Category? Category { get; set; }

        [JsonProperty("author")]
        public Author? Author { get; set; }

        [JsonProperty("date")]
        public DateTime Date { get; set; }

        [JsonProperty("description")]
        public string? Description { get; set; }

        [JsonProperty("version")]
        public string? Version { get; set; }

        [JsonProperty("changelog")]
        public string? Changelog { get; set; }

        [JsonProperty("files")] public List<File> Files { get; set; } = new();

        [JsonProperty("screenshots")] public List<Screenshot> Screenshots { get; set; } = new();

        [JsonProperty("primaryScreenshot")]
        public PrimaryScreenshot PrimaryScreenshot { get; set; } = new();

        [JsonProperty("downloads")]
        public int Downloads { get; set; }

        [JsonProperty("comments")]
        public int Comments { get; set; }

        [JsonProperty("reviews")]
        public int Reviews { get; set; }

        [JsonProperty("views")]
        public int Views { get; set; }

        [JsonProperty("tags")] public List<string?> Tags { get; set; } = new ();

        [JsonProperty("locked")]
        public bool Locked { get; set; }

        [JsonProperty("hidden")]
        public bool Hidden { get; set; }

        [JsonProperty("pinned")]
        public bool Pinned { get; set; }

        [JsonProperty("featured")]
        public bool Featured { get; set; }

        [JsonProperty("url")]
        public string? Url { get; set; }

        [JsonProperty("topic")] public Topic Topic { get; set; } = new();
    }


    }
}
