using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentFTP;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Nettle;
using SharpCompress.Compressors.LZMA;
using Wabbajack.Common;
using Wabbajack.Lib.AuthorApi;
using Wabbajack.Server.DataLayer;
using Wabbajack.Server.DTOs;
using Wabbajack.Server.Services;

namespace Wabbajack.BuildServer.Controllers
{
    [Authorize(Roles = "Author")]
    [Route("/authored_files")]
    public class AuthoredFiles : ControllerBase
    {
        private SqlService _sql;
        private ILogger<AuthoredFiles> _logger;
        private AppSettings _settings;
        private CDNMirrorList _mirrorList;
        private DiscordWebHook _discord;


        public AuthoredFiles(ILogger<AuthoredFiles> logger, SqlService sql, AppSettings settings, CDNMirrorList mirrorList, DiscordWebHook discord)
        {
            _sql = sql;
            _logger = logger;
            _settings = settings;
            _mirrorList = mirrorList;
            _discord = discord;
        }

        [HttpPut]
        [Route("{serverAssignedUniqueId}/part/{index}")]
        public async Task<IActionResult> UploadFilePart(string serverAssignedUniqueId, long index)
        {
            var user = User.FindFirstValue(ClaimTypes.Name);
            var definition = await _sql.GetCDNFileDefinition(serverAssignedUniqueId);
            if (definition.Author != user)
                return Forbid("File Id does not match authorized user");
            _logger.Log(LogLevel.Information, $"Uploading File part {definition.OriginalFileName} - ({index} / {definition.Parts.Length})");

            await _sql.TouchAuthoredFile(definition);
            var part = definition.Parts[index];

            await using var ms = new MemoryStream();
            await Request.Body.CopyToLimitAsync(ms, part.Size);
            ms.Position = 0;
            if (ms.Length != part.Size)
                return BadRequest($"Couldn't read enough data for part {part.Size} vs {ms.Length}");

            var hash = ms.xxHash();
            if (hash != part.Hash)
                return BadRequest($"Hashes don't match for index {index}. Sizes ({ms.Length} vs {part.Size}). Hashes ({hash} vs {part.Hash}");

            ms.Position = 0;
            await UploadAsync(ms, $"{definition.MungedName}/parts/{index}");
            return Ok(part.Hash.ToBase64());
        }
        
        [HttpPut]
        [Route("create")]
        public async Task<IActionResult> CreateUpload()
        {
            var user = User.FindFirstValue(ClaimTypes.Name);

            var data = await Request.Body.ReadAllTextAsync();
            var definition = data.FromJsonString<CDNFileDefinition>();
            
            _logger.Log(LogLevel.Information, $"Creating File upload {definition.OriginalFileName}");

            definition = await _sql.CreateAuthoredFile(definition, user);
            
            using (var client = await GetBunnyCdnFtpClient())
            {
                await client.CreateDirectoryAsync($"{definition.MungedName}");
                await client.CreateDirectoryAsync($"{definition.MungedName}/parts");
            }

            await _discord.Send(Channel.Ham,
                new DiscordMessage() {Content = $"{user} has started uploading {definition.OriginalFileName} ({definition.Size.ToFileSizeString()})"});
            
            return Ok(definition.ServerAssignedUniqueId);
        }
        
        [HttpPut]
        [Route("{serverAssignedUniqueId}/finish")]
        public async Task<IActionResult> CreateUpload(string serverAssignedUniqueId)
        {
            var user = User.FindFirstValue(ClaimTypes.Name);
            var definition = await _sql.GetCDNFileDefinition(serverAssignedUniqueId);
            if (definition.Author != user)
                return Forbid("File Id does not match authorized user");
            _logger.Log(LogLevel.Information, $"Finalizing file upload {definition.OriginalFileName}");

            await _sql.Finalize(definition);
            
            await using var ms = new MemoryStream();
            await using (var gz = new GZipStream(ms, CompressionLevel.Optimal, true))
            {
                definition.ToJson(gz);
            }
            ms.Position = 0;
            await UploadAsync(ms, $"{definition.MungedName}/definition.json.gz");
            
            await _discord.Send(Channel.Ham,
                new DiscordMessage {Content = $"{user} has finished uploading {definition.OriginalFileName} ({definition.Size.ToFileSizeString()})"});

            var host = Consts.TestMode ? "test-files" : "authored-files";
            return Ok($"https://{host}.wabbajack.org/{definition.MungedName}");
        }

        private async Task<FtpClient> GetBunnyCdnFtpClient()
        {
            var info = await BunnyCdnFtpInfo.GetCreds(StorageSpace.AuthoredFiles);
            var client = new FtpClient(info.Hostname) {Credentials = new NetworkCredential(info.Username, info.Password)};
            await client.ConnectAsync();
            return client;
        }

        private async Task UploadAsync(Stream stream, string path)
        {
            using var client = await GetBunnyCdnFtpClient();
            await client.UploadAsync(stream, path);
        }

        [HttpDelete]
        [Route("{serverAssignedUniqueId}")]
        public async Task<IActionResult> DeleteUpload(string serverAssignedUniqueId)
        {
            var user = User.FindFirstValue(ClaimTypes.Name);
            var definition = await _sql.GetCDNFileDefinition(serverAssignedUniqueId);
            if (definition.Author != user)
                return Forbid("File Id does not match authorized user");
            _logger.Log(LogLevel.Information, $"Finalizing file upload {definition.OriginalFileName}");

            await DeleteFolderOrSilentlyFail($"{definition.MungedName}");

            await _sql.DeleteFileDefinition(definition);
            return Ok();
        }

        private async Task DeleteFolderOrSilentlyFail(string path)
        {
            try
            {
                using var client = await GetBunnyCdnFtpClient();
                await client.DeleteDirectoryAsync(path);
            }
            catch (Exception)
            {
                _logger.Log(LogLevel.Information, $"Delete failed for {path}");
            }
        }
        
        private static readonly Func<object, string> HandleGetListTemplate = NettleEngine.GetCompiler().Compile(@"
            <html><body>
                <table>
                {{each $.files }}
                <tr><td><a href='https://authored-files.wabbajack.org/{{$.MungedName}}'>{{$.OriginalFileName}}</a></td><td>{{$.Size}}</td><td>{{$.LastTouched}}</td><td>{{$.Finalized}}</td><td>{{$.Author}}</td></tr>
                {{/each}}
                </table>
            </body></html>
        ");



        [HttpGet]
        [AllowAnonymous]
        [Route("")]
        public async Task<ContentResult> UploadedFilesGet()
        {
            var files = await _sql.AllAuthoredFiles();
            var response = HandleGetListTemplate(new {files});
            return new ContentResult
            {
                ContentType = "text/html",
                StatusCode = (int) HttpStatusCode.OK,
                Content = response
            };
        }

        [HttpGet]
        [AllowAnonymous]
        [Route("mirrors")]
        public async Task<IActionResult> GetMirrorList()
        {
            Response.Headers.Add("x-last-updated", _mirrorList.LastUpdate.ToString(CultureInfo.InvariantCulture));
            return Ok(_mirrorList.Mirrors);
        }
        

    }
}
