using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentFTP;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SharpCompress.Compressors.LZMA;
using Wabbajack.Common;
using Wabbajack.Lib.AuthorApi;
using Wabbajack.Server.DataLayer;
using Wabbajack.Server.DTOs;

namespace Wabbajack.BuildServer.Controllers
{
    [Route("/authored_files")]
    public class AuthoredFiles : ControllerBase
    {
        private SqlService _sql;
        private ILogger<AuthoredFiles> _logger;
        private AppSettings _settings;

        public AuthoredFiles(ILogger<AuthoredFiles> logger, SqlService sql, AppSettings settings)
        {
            _sql = sql;
            _logger = logger;
            _settings = settings;
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
            
            return Ok($"https://{_settings.BunnyCDN_StorageZone}.b-cdn.net/{definition.MungedName}");
        }

        private async Task<FtpClient> GetBunnyCdnFtpClient()
        {
            var info = Utils.FromEncryptedJson<BunnyCdnFtpInfo>("bunny-cdn-ftp-info");
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

    }
}
