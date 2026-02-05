using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Wabbajack.DTOs.API;

/// <summary>
/// Full pre-install information for a modlist.
/// </summary>
public record ModlistPreInstallInfo(
    [property: JsonPropertyName("sessionId")] string SessionId,
    [property: JsonPropertyName("modlist")] ModlistBasicInfo Modlist,
    [property: JsonPropertyName("requirements")] InstallationRequirements Requirements,
    [property: JsonPropertyName("warnings")] List<PreInstallWarning> Warnings);

/// <summary>
/// Basic information extracted from the modlist.
/// </summary>
public record ModlistBasicInfo(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("author")] string Author,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("gameType")] string GameType,
    [property: JsonPropertyName("gameDisplayName")] string GameDisplayName,
    [property: JsonPropertyName("isNsfw")] bool IsNsfw,
    [property: JsonPropertyName("website")] string? Website,
    [property: JsonPropertyName("readme")] string? Readme);

/// <summary>
/// Installation requirements calculated from the modlist.
/// </summary>
public record InstallationRequirements(
    [property: JsonPropertyName("archiveCount")] int ArchiveCount,
    [property: JsonPropertyName("totalArchiveSize")] long TotalArchiveSize,
    [property: JsonPropertyName("directiveCount")] int DirectiveCount,
    [property: JsonPropertyName("totalInstalledSize")] long TotalInstalledSize,
    [property: JsonPropertyName("estimatedTempSpace")] long EstimatedTempSpace,
    [property: JsonPropertyName("gameInstalled")] bool GameInstalled,
    [property: JsonPropertyName("gamePath")] string? GamePath,
    [property: JsonPropertyName("manualDownloadCount")] int ManualDownloadCount,
    [property: JsonPropertyName("nonAutomaticDownloadCount")] int NonAutomaticDownloadCount);

/// <summary>
/// A warning to display before installation.
/// </summary>
public record PreInstallWarning(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("message")] string Message);

/// <summary>
/// Request to prepare a modlist for installation.
/// </summary>
public record ModlistPrepareRequest(
    [property: JsonPropertyName("downloadUrl")] string DownloadUrl,
    [property: JsonPropertyName("machineUrl")] string MachineUrl);

/// <summary>
/// Status of a modlist preparation operation.
/// </summary>
public record ModlistPrepareStatus(
    [property: JsonPropertyName("sessionId")] string SessionId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("progress")] double Progress,
    [property: JsonPropertyName("error")] string? Error);
