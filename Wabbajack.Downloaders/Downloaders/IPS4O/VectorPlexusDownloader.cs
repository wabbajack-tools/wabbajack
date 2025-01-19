using System;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.Logins;
using Wabbajack.Networking.Http.Interfaces;

namespace Wabbajack.Downloaders.IPS4OAuth2Downloader;

public class
    VectorPlexusDownloader : AIPS4OAuth2Downloader<VectorPlexusDownloader, VectorPlexusLoginState, VectorPlexus>
{
    public VectorPlexusDownloader(ILogger<VectorPlexusDownloader> logger,
        ITokenProvider<VectorPlexusLoginState> loginInfo, HttpClient client,
        IHttpDownloader downloader, ApplicationInfo appInfo)
        : base(logger, loginInfo, client, downloader, appInfo, new Uri("https://vectorplexis.com"), "Vector Plexus")
    {
    }
}