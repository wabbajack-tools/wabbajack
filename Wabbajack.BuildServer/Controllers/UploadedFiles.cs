using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using FluentFTP;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Nettle;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Wabbajack.BuildServer.Model.Models;
using Wabbajack.BuildServer.Models;
using Wabbajack.BuildServer.Models.JobQueue;
using Wabbajack.BuildServer.Models.Jobs;
using Wabbajack.Common;
using Path = Alphaleonis.Win32.Filesystem.Path;
using AlphaFile = Alphaleonis.Win32.Filesystem.File;

namespace Wabbajack.BuildServer.Controllers
{
    public class UploadedFiles : AControllerBase<UploadedFiles>
    {
        private static ConcurrentDictionary<string, AsyncLock> _writeLocks = new ConcurrentDictionary<string, AsyncLock>();
        private AppSettings _settings;
        
        public UploadedFiles(ILogger<UploadedFiles> logger, AppSettings settings, SqlService sql) : base(logger, sql)
        {
            _settings = settings;
        }

        [HttpPut]
        [Route("upload_file/{Name}/start")]
        public async Task<IActionResult> UploadFileStreaming(string Name)
        {
            var guid = Guid.NewGuid();
            var key = Encoding.UTF8.GetBytes($"{Path.GetFileNameWithoutExtension(Name)}|{guid.ToString()}|{Path.GetExtension(Name)}").ToHex();
            
            _writeLocks.GetOrAdd(key, new AsyncLock());

            await using var fs = _settings.TempPath.Combine(key).Create();
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
            
            var ms = new MemoryStream();
            await Request.Body.CopyToAsync(ms);
            ms.Position = 0;

            Utils.Log($"Writing {ms.Length} at position {Offset} in ingest file {Key}");

            long position;
            using (var _ = await _writeLocks[Key].WaitAsync())
            {
                await using var file = _settings.TempPath.Combine(Key).WriteShared();
                file.Position = Offset;
                await ms.CopyToAsync(file);
                position = Offset + ms.Length;
            }

            Utils.Log($"Wrote {ms.Length} as position {Offset} result {position}");

            return Ok(position);
        }

        [Authorize]
        [HttpGet]
        [Route("clean_http_uploads")]
        public async Task<IActionResult> CleanUploads()
        {
            var files = await SQL.AllUploadedFiles();
            var seen = new HashSet<string>();
            var duplicate = new List<UploadedFile>();

            foreach (var file in files)
            {
                if (seen.Contains(file.Name))
                    duplicate.Add(file);
                else
                    seen.Add(file.Name);
            }

            using (var client = new FtpClient("storage.bunnycdn.com"))
            {
                client.Credentials = new NetworkCredential(_settings.BunnyCDN_User, _settings.BunnyCDN_Password);
                await client.ConnectAsync();

                foreach (var dup in duplicate)
                {
                    var final_path = Path.Combine("public", "files", dup.MungedName);
                    Utils.Log($"Cleaning upload {final_path}");
                    
                    if (AlphaFile.Exists(final_path))
                        AlphaFile.Delete(final_path);

                    if (await client.FileExistsAsync(dup.MungedName))
                        await client.DeleteFileAsync(dup.MungedName);
                    await SQL.DeleteUploadedFile(dup.Id);
                }
            }

            return Ok(new {Remain = seen.ToArray(), Deleted = duplicate.ToArray()}.ToJson());
        }
        

        [HttpPut]
        [Route("upload_file/{Key}/finish/{xxHashAsHex}")]
        public async Task<IActionResult> UploadFileFinish(string Key, string xxHashAsHex)
        {
            var expectedHash = Hash.FromHex(xxHashAsHex);
            var user = User.FindFirstValue(ClaimTypes.Name);
            if (!Key.All(a => HexChars.Contains(a)))
                return BadRequest("NOT A VALID FILENAME");
            var parts = Encoding.UTF8.GetString(Key.FromHex()).Split('|');
            var finalName = $"{parts[0]}-{parts[1]}{parts[2]}";
            var originalName = $"{parts[0]}{parts[2]}";

            var finalPath = "public".RelativeTo(AbsolutePath.EntryPoint).Combine("files", finalName);
            await _settings.TempPath.Combine(Key).MoveToAsync(finalPath);
            
            var hash = await finalPath.FileHashAsync();

            if (expectedHash != hash)
            {
                finalPath.Delete();
                return BadRequest($"Bad Hash, Expected: {expectedHash} Got: {hash}");
            }

            _writeLocks.TryRemove(Key, out var _);
            var record = new UploadedFile
            {
                Id = Guid.Parse(parts[1]),
                Hash = hash, 
                Name = originalName, 
                Uploader = user, 
                Size = finalPath.Size,
                CDNName = "wabbajackpush"
            };
            await SQL.AddUploadedFile(record);
            await SQL.EnqueueJob(new Job
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
            var files = await SQL.AllUploadedFiles();
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

        [HttpGet]
        [Route("uploaded_files/list")]
        [Authorize]
        public async Task<IActionResult> ListMyFiles()
        {
            var user = User.FindFirstValue(ClaimTypes.Name);
            Utils.Log($"List Uploaded Files {user}");
            var files = await SQL.AllUploadedFilesForUser(user);
            return Ok(files.OrderBy(f => f.UploadDate).Select(f => f.MungedName ).ToArray().ToJson());
        }

        [HttpDelete]
        [Route("uploaded_files/{name}")]
        [Authorize]
        public async Task<IActionResult> DeleteMyFile(string name)
        {
            var user = User.FindFirstValue(ClaimTypes.Name);
            Utils.Log($"Delete Uploaded File {user} {name}");
            var files = await SQL.AllUploadedFilesForUser(user);
            
            var to_delete = files.First(f => f.MungedName == name);
            
            if (AlphaFile.Exists(Path.Combine("public", "files", to_delete.MungedName)))
                AlphaFile.Delete(Path.Combine("public", "files", to_delete.MungedName));


            if (_settings.BunnyCDN_User != "TEST" || _settings.BunnyCDN_Password != "TEST")
            {
                using (var client = new FtpClient("storage.bunnycdn.com"))
                {
                    client.Credentials = new NetworkCredential(_settings.BunnyCDN_User, _settings.BunnyCDN_Password);
                    await client.ConnectAsync();
                    if (await client.FileExistsAsync(to_delete.MungedName))
                        await client.DeleteFileAsync(to_delete.MungedName);

                }
            }

            await SQL.DeleteUploadedFile(to_delete.Id);
            return Ok($"Deleted {to_delete.MungedName}");
        }

        [HttpGet]
        [Route("ingest/uploaded_files/{name}")]
        [Authorize]
        public async Task<IActionResult> IngestMongoDB(string name)
        {
            var fullPath = name.RelativeTo((AbsolutePath)_settings.TempFolder);
            await using var fs = fullPath.OpenRead();
            
            var files = new List<UploadedFile>();
            using var rdr = new JsonTextReader(new StreamReader(fs)) {SupportMultipleContent = true};

            while (await rdr.ReadAsync())
            {
                dynamic obj = await JObject.LoadAsync(rdr);


                var uf = new UploadedFile
                {
                    Id = Guid.Parse((string)obj._id),
                    Name = obj.Name,
                    Size = long.Parse((string)(obj.Size["$numberLong"] ?? obj.Size["$numberInt"])),
                    Hash = Hash.FromBase64((string)obj.Hash),
                    Uploader = obj.Uploader,
                    UploadDate = long.Parse(((string)obj.UploadDate["$date"]["$numberLong"]).Substring(0, 10)).AsUnixTime(),
                    CDNName = obj.CDNName
                };
                files.Add(uf);
                await SQL.AddUploadedFile(uf);
            }
            return Ok(files.Count);
        }
    }
}
