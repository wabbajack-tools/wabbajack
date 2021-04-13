using System;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using FluentFTP;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Nettle;
using Wabbajack.Common;
using Wabbajack.Server.DataLayer;

namespace Wabbajack.BuildServer.Controllers
{
    [Authorize(Roles="Author")]
    [Route("/author_controls")]
    public class AuthorControls : ControllerBase
    {
        private ILogger<AuthorControls> _logger;
        private SqlService _sql;

        public AuthorControls(ILogger<AuthorControls> logger, SqlService sql)
        {
            _logger = logger;
            _sql = sql;
        }
        
        [Route("login/{authorKey}")]
        [AllowAnonymous]
        public async Task<IActionResult> Login(string authorKey)
        {
            Response.Cookies.Append(ApiKeyAuthenticationHandler.ApiKeyHeaderName, authorKey);
            return Redirect($"{Consts.WabbajackBuildServerUri}author_controls/home");
        }
        
        private static async Task<string> HomePageTemplate(object o)
        {
            var data = await AbsolutePath.EntryPoint.Combine(@"Controllers\Templates\AuthorControls.html")
                .ReadAllTextAsync();
            var func = NettleEngine.GetCompiler().Compile(data);
            return func(o);
        }

        [Route("home")]
        [Authorize("")]
        public async Task<IActionResult> HomePage()
        {
            var user = User.FindFirstValue(ClaimTypes.Name);
            var files = (await _sql.AllAuthoredFiles())
                .Where(af => af.Author == user)
                .Select(af => new
                {
                    Size = af.Size.FileSizeToString(),
                    OriginalSize = af.Size,
                    Name = af.OriginalFileName,
                    MangledName = af.MungedName,
                    UploadedDate = af.LastTouched
                })
                .OrderBy(f => f.Name)
                .ThenBy(f => f.UploadedDate)
                .ToList();

            var result = HomePageTemplate(new
            {
                User = user,
                TotalUsage = files.Select(f => f.OriginalSize).Sum().ToFileSizeString(),
                WabbajackFiles = files.Where(f => f.Name.EndsWith(Consts.ModListExtensionString)),
                OtherFiles = files.Where(f => !f.Name.EndsWith(Consts.ModListExtensionString))
            });
            
            return new ContentResult {
                ContentType = "text/html", 
                StatusCode = (int)HttpStatusCode.OK, 
                Content = await result};
        }
    }
}
