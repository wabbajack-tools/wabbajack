using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Wabbajack.Common;
using Wabbajack.Lib.Downloaders;

namespace Wabbajack.BuildServer.Controllers
{
    [ApiController]
    [Authorize(Roles = "User")]
    [Route("/site-integration")]
    public class SiteIntegration : ControllerBase
    {
        private ILogger<SiteIntegration> _logger;

        public SiteIntegration(ILogger<SiteIntegration> logger)
        {
            _logger = logger;
        }

        private HashSet<string> Allowed = new HashSet<string>
        { "loverslabcookies", "deadlystream", "tesall", "tesalliance", "vectorplexus"};
        [Route("auth-info/{site}")]
        public async Task<IActionResult> GetAuthInfo(string site)
        {
            if (!Allowed.Contains(site))
            {
                return BadRequest("No key found");
            }

            return Ok(Encoding.UTF8.GetString(await Utils.FromEncryptedData(site)));
        }
    }
}
