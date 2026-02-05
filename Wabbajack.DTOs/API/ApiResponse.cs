using System;
using System.Text.Json.Serialization;

namespace Wabbajack.DTOs.API;

/// <summary>
/// Standard wrapper for all API responses.
/// </summary>
/// <typeparam name="T">The type of data returned.</typeparam>
public record ApiResponse<T>(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("data")] T? Data,
    [property: JsonPropertyName("error")] string? Error)
{
    public static ApiResponse<T> Ok(T data) => new(true, data, null);
    public static ApiResponse<T> Fail(string error) => new(false, default, error);
}

/// <summary>
/// Response from the /api/hello endpoint.
/// </summary>
public record HelloResponse(
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("timestamp")] DateTime Timestamp);

/// <summary>
/// Information about an installed game.
/// </summary>
public record GameInfo(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("slug")] string Slug,
    [property: JsonPropertyName("installed")] bool Installed,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("path")] string Path);

/// <summary>
/// Server-Sent Event message types.
/// </summary>
public enum ServerEventType
{
    Connected,
    StatusUpdate,
    Progress,
    Error,
    Heartbeat
}

/// <summary>
/// A Server-Sent Event message.
/// </summary>
public record ServerEvent(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("timestamp")] DateTime Timestamp,
    [property: JsonPropertyName("data")] object? Data);
