using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Nettle;
using Wabbajack.BuildServer.Models;
using Wabbajack.Common;

namespace Wabbajack.BuildServer.Controllers
{
    public class UploadedFiles : AControllerBase<UploadedFiles>
    {
        public UploadedFiles(ILogger<UploadedFiles> logger, DBContext db) : base(logger, db)
        {
  
        }

        [HttpPost]
        [Authorize]
        [Route("upload_file")]
        public async Task<IActionResult> UploadFile(IList<IFormFile> files)
        {
            var user = User.FindFirstValue(ClaimTypes.Name);
            UploadedFile result = null;
            result = await UploadedFile.Ingest(Db, files.First(), user);
            return Ok(result.Uri.ToString());
        }
        
        private static readonly Func<object, string> HandleGetListTemplate = NettleEngine.GetCompiler().Compile(@"
            <html><body>
                <table>
                {{each $.files }}
                <tr><td><a href='{{$.Link}}'>{{$.Name}}</a></td><td>{{$.Size}}</td><td>{{$.Date}}</td><td>{{$.Uploader}}</td></tr>
                {{/each}}
                </table>
            </body></html>
        ");

        [HttpGet]
        [Route("uploaded_files")]
        public async Task<ContentResult> UploadedFilesGet()
        {
            var files = await Db.UploadedFiles.AsQueryable().OrderByDescending(f => f.UploadDate).ToListAsync();
            var response = HandleGetListTemplate(new
            {
                files = files.Select(file => new
                {
                    Link = file.Uri,
                    Size = file.Size.ToFileSizeString(),
                    file.Name,
                    Date = file.UploadDate,
                    file.Uploader
                })
                
            });
            return new ContentResult
            {
                ContentType = "text/html",
                StatusCode = (int) HttpStatusCode.OK,
                Content = response
            };
        }
    }
}
