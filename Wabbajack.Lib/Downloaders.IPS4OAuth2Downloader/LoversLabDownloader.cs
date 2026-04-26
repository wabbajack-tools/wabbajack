using System;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.Logins;
using Wabbajack.Networking.Http.Interfaces;

namespace Wabbajack.Downloaders.IPS4OAuth2Downloader;

public class LoversLabDownloader : AIPS4OAuth2Downloader<LoversLabDownloader, LoversLabLoginState, LoversLab>
{
    public LoversLabDownloader(ILogger<LoversLabDownloader> logger, ITokenProvider<LoversLabLoginState> loginInfo,
        HttpClient client,
        IHttpDownloader downloader, ApplicationInfo appInfo) : base(logger, loginInfo, client, downloader, appInfo,
        new Uri("https://api.loverslab.com"), "Lovers Lab")
    {
    }
}