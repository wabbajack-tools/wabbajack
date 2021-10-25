using System.Collections.Generic;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.Server.Lib.DTOs;

namespace Wabbajack.Server.Lib.TokenProviders;

public interface IFtpSiteCredentials : ITokenProvider<Dictionary<StorageSpace, FtpSite>>
{
}