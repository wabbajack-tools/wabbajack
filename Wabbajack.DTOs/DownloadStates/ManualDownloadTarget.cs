using System;

namespace Wabbajack.DTOs.DownloadStates;

/// <summary>
///     Where a user has to go to fetch an archive by hand: the page to open, the site it belongs to, and
///     what to do once there.
/// </summary>
public record ManualDownloadTarget(Uri Url, string SiteName, string Instructions);
