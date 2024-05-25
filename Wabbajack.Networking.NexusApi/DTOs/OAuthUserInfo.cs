using System;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Octokit;

namespace Wabbajack.Networking.NexusApi.DTOs;

public record OAuthUserInfo
{
    /// <summary>
    /// Gets the User ID.
    /// </summary>
    [JsonPropertyName("sub")]
    public string Sub { get; set; } = string.Empty;

    /// <summary>
    /// Gets the User Name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets the avatar url.
    /// </summary>
    [JsonPropertyName("avatar")]
    public Uri? Avatar { get; set; }

    /// <summary>
    /// Gets an array of membership roles.
    /// </summary>
    [JsonPropertyName("membership_roles")]
    public string[] MembershipRoles { get; set; } = [];
}