using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FluentFTP;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Nettle;
using Wabbajack.Common;
using Wabbajack.DTOs.CDN;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Server.DataLayer;
using Wabbajack.Server.DTOs;
using Wabbajack.Server.Services;
using Wabbajack.Server.TokenProviders;

namespace Wabbajack.BuildServer.Controllers
{
    [Authorize(Roles = "Author")]
    [Route("/authored_files")]
    public class AuthoredFiles : ControllerBase
    {
        private SqlService _sql;
        private ILogger<AuthoredFiles> _logger;
        private AppSettings _settings;
        private DiscordWebHook _discord;
        
        private readonly IFtpSiteCredentials _ftpCreds;

        
        private readonly DTOSerializer _dtos;


        public AuthoredFiles(ILogger<AuthoredFiles> logger, SqlService sql, AppSettings settings, DiscordWebHook discord, 
            DTOSerializer dtos, IFtpSiteCredentials ftpCreds)
        {
            _sql = sql;
            _logger = logger;
            _settings = settings;
            _discord = discord;
            _dtos = dtos;
            _ftpCreds = ftpCreds;
        }

        [HttpPut]
        [Route("{serverAssignedUniqueId}/part/{index}")]
        public async Task<IActionResult> UploadFilePart(CancellationToken token, string serverAssignedUniqueId, long index)
        {
            var user = User.FindFirstValue(ClaimTypes.Name);
            var definition = await _sql.GetCDNFileDefinition(serverAssignedUniqueId);
            if (definition.Author != user)
                return Forbid("File Id does not match authorized user");
            _logger.Log(LogLevel.Information, $"Uploading File part {definition.OriginalFileName} - ({index} / {definition.Parts.Length})");

            await _sql.TouchAuthoredFile(definition);
            var part = definition.Parts[index];

            await using var ms = new MemoryStream();
            await Request.Body.CopyToLimitAsync(ms, (int)part.Size, token);
            ms.Position = 0;
            if (ms.Length != part.Size)
                return BadRequest($"Couldn't read enough data for part {part.Size} vs {ms.Length}");

            var hash = await ms.Hash(token);
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

            var definition = (await _dtos.DeserializeAsync<FileDefinition>(Request.Body))!;
            
            _logger.Log(LogLevel.Information, "Creating File upload {originalFileName}", definition.OriginalFileName);

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
                await _dtos.Serialize(definition, gz);
            }
            ms.Position = 0;
            await UploadAsync(ms, $"{definition.MungedName}/definition.json.gz");
            
            await _discord.Send(Channel.Ham,
                new DiscordMessage {Content = $"{user} has finished uploading {definition.OriginalFileName} ({definition.Size.ToFileSizeString()})"});

            var host = _settings.TestMode ? "test-files" : "authored-files";
            return Ok($"https://{host}.wabbajack.org/{definition.MungedName}");
        }

        private async Task<FtpClient> GetBunnyCdnFtpClient()
        {
            var info = (await _ftpCreds.Get())[StorageSpace.AuthoredFiles];
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
            await _discord.Send(Channel.Ham, new DiscordMessage() {Content = $"{user} is deleting {definition.MungedName}, {definition.Size.ToFileSizeString()} to be freed"});
            _logger.Log(LogLevel.Information, $"Deleting upload {definition.OriginalFileName}");

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
    }
}
