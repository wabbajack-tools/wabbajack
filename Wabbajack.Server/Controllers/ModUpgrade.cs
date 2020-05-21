using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Server.DataLayer;
using Wabbajack.Server.Services;

namespace Wabbajack.BuildServer.Controllers
{
    [ApiController]
    public class ModUpgrade : ControllerBase
    {
        private ILogger<ModUpgrade> _logger;
        private SqlService _sql;
        private DiscordWebHook _discord;
        private AppSettings _settings;

        public ModUpgrade(ILogger<ModUpgrade> logger, SqlService sql, DiscordWebHook discord, AppSettings settings)
        {
            _logger = logger;
            _sql = sql;
            _discord = discord;
            _settings = settings;
        }
        
        [HttpPost]
        [Route("/mod_upgrade")]
        public async Task<IActionResult> PostModUpgrade()
        {
            var request = (await Request.Body.ReadAllTextAsync()).FromJsonString<ModUpgradeRequest>();
            if (!request.IsValid)
            {
                return BadRequest("Invalid mod upgrade");
            }

            var oldDownload = await _sql.GetOrEnqueueArchive(request.OldArchive);
            var newDownload = await _sql.GetOrEnqueueArchive(request.NewArchive);

            _logger.Log(LogLevel.Information, $"Upgrade requested from {oldDownload.Archive.Hash} to {newDownload.Archive.Hash}");
            var patch = await _sql.FindOrEnqueuePatch(oldDownload.Id, newDownload.Id);
            if (patch.Finished.HasValue)
            {
                if (patch.PatchSize != 0)
                {
                    return
                        Ok(
                            $"https://{_settings.BunnyCDN_StorageZone}.b-cdn.net/{Consts.ArchiveUpdatesCDNFolder}/{request.OldArchive.Hash.ToHex()}_{request.NewArchive.Hash.ToHex()}");
                }

                return NotFound("Patch creation failed");
            }
            
            // Still processing
            return Accepted();
        }

    }
}
