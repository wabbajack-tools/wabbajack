using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Wabbajack.Common;
using Wabbajack.Downloaders.Interfaces;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.Validation;

namespace Wabbajack.Downloaders.ManualSources;

public class GoogleDriveSource : AManualSourceDownloader<GoogleDrive>, IUrlDownloader, IProxyable
{
    private static readonly Regex GDriveRegex = new("((?<=id=)[a-zA-Z0-9_-]*)|(?<=\\/file\\/d\\/)[a-zA-Z0-9_-]*",
        RegexOptions.Compiled);

    private readonly ILogger<GoogleDriveSource> _logger;

    public GoogleDriveSource(ILogger<GoogleDriveSource> logger)
    {
        _logger = logger;
    }

    protected override string Reason => "Google Drive files have to be downloaded in a browser";

    public override bool IsAllowed(ServerAllowList allowList, IDownloadState state)
    {
        return allowList.GoogleIDs.Contains(((GoogleDrive)state).Id);
    }

    public IDownloadState? Parse(Uri uri)
    {
        if (uri.Host != "drive.google.com") return null;
        var match = GDriveRegex.Match(uri.ToString());
        if (match.Success)
            return new GoogleDrive { Id = match.ToString() };
        _logger.LogWarning($"Tried to parse drive.google.com Url but couldn't get an id from: {uri}");
        return null;
    }

    public Uri UnParse(IDownloadState state)
    {
        return new Uri(
            $"https://drive.google.com/uc?id={(state as GoogleDrive)?.Id}&export=download");
    }

    public override IDownloadState? Resolve(IReadOnlyDictionary<string, string> iniData)
    {
        if (iniData.ContainsKey("directURL") && Uri.TryCreate(iniData["directURL"].CleanIniString(), UriKind.Absolute, out var uri))
            return Parse(uri);
        return null;
    }

    public override IEnumerable<string> MetaIni(Archive a, GoogleDrive state)
    {
        return new[] { $"directURL=https://drive.google.com/uc?id={state.Id}&export=download" };
    }
}
