using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Text;
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
using Wabbajack.BuildServer.Models.JobQueue;
using Wabbajack.BuildServer.Models.Jobs;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace Wabbajack.BuildServer.Controllers
{
    public class UploadedFiles : AControllerBase<UploadedFiles>
    {
        private AppSettings _settings;

        public UploadedFiles(ILogger<UploadedFiles> logger, DBContext db, AppSettings settings) : base(logger, db)
        {
            _settings = settings;
        }

        [HttpPut]
        [Route("upload_file/{Name}/start")]
        public async Task<IActionResult> UploadFileStreaming(string Name)
        {
            var guid = Guid.NewGuid();
            var key = Encoding.UTF8.GetBytes($"{Path.GetFileNameWithoutExtension(Name)}|{guid.ToString()}|{Path.GetExtension(Name)}").ToHex();
            System.IO.File.Create(Path.Combine("public", "files", key)).Close();
            Utils.Log($"Starting Ingest for {key}");
            return Ok(key);
        }

        static private HashSet<char> HexChars = new HashSet<char>("abcdef1234567890");
        [HttpPut]
        [Route("upload_file/{Key}/data/{Offset}")]
        public async Task<IActionResult> UploadFilePart(string Key, long Offset)
        {
            if (!Key.All(a => HexChars.Contains(a)))
                return BadRequest("NOT A VALID FILENAME");
            Utils.Log($"Writing at position {Offset} in ingest file {Key}");
            await using (var file = System.IO.File.Open(Path.Combine("public", "files", Key), FileMode.Open, FileAccess.Write, FileShare.ReadWrite))
            {
                file.Position = Offset;
                await Request.Body.CopyToAsync(file);
                return Ok(file.Position.ToString());
            }
        }

        [HttpPut]
        [Route("upload_file/{Key}/finish")]
        public async Task<IActionResult> UploadFileFinish(string Key)
        {
            var user = User.FindFirstValue(ClaimTypes.Name);
            if (!Key.All(a => HexChars.Contains(a)))
                return BadRequest("NOT A VALID FILENAME");
            var parts = Encoding.UTF8.GetString(Key.FromHex()).Split('|');
            var final_name = $"{parts[0]}-{parts[1]}{parts[2]}";
            var original_name = $"{parts[0]}{parts[2]}";

            var final_path = Path.Combine("public", "files", final_name);
            System.IO.File.Move(Path.Combine("public", "files", Key), final_path);
            var hash = await final_path.FileHashAsync();
            

            var record = new UploadedFile
            {
                Id = parts[1],
                Hash = hash, 
                Name = original_name, 
                Uploader = user, 
                Size = new FileInfo(final_path).Length,
                CDNName = "wabbajackpush"
            };
            await Db.UploadedFiles.InsertOneAsync(record);
            await Db.Jobs.InsertOneAsync(new Job
            {
                Priority = Job.JobPriority.High, Payload = new UploadToCDN {FileId = record.Id}
            });

            
            return Ok(record.Uri);
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
