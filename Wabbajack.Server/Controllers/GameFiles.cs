#nullable enable
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Server.DataLayer;
using Wabbajack.Server.Services;

namespace Wabbajack.BuildServer.Controllers
{
    [Route("/game_files")]
    public class EnqueueGameFiles : ControllerBase
    {
        private readonly ILogger<EnqueueGameFiles> _logger;
        private readonly SqlService _sql;
        private readonly QuickSync _quickSync;

        public EnqueueGameFiles(ILogger<EnqueueGameFiles> logger, SqlService sql, QuickSync quickSync)
        {
            _logger = logger;
            _sql = sql;
            _quickSync = quickSync;
        }
        
        
        [Authorize(Roles = "Author")]
        [HttpGet("enqueue")]
        public async Task<IActionResult> Enqueue()
        {
            var games = GameRegistry.Games.Where(g => g.Value.IsInstalled).Select(g => g.Value).ToList();
            _logger.Log(LogLevel.Information, $"Found {games.Count} installed games");

            var files = games.SelectMany(game =>
                game.GameLocation().EnumerateFiles(true).Select(file => new {File = file, Game = game})).ToList();
            
            _logger.Log(LogLevel.Information, $"Found {files.Count} game files");
            
            using var queue = new WorkQueue();
            var hashed = (await files.PMap(queue, async pair =>
            {
                var hash = await pair.File.FileHashCachedAsync();
                if (hash == null) return null;
                
                return await _sql.GetOrEnqueueArchive(new Archive(new GameFileSourceDownloader.State
                {
                    Game = pair.Game.Game,
                    GameFile = pair.File.RelativeTo(pair.Game.GameLocation()),
                    GameVersion = pair.Game.InstalledVersion,
                    Hash = hash.Value
                }) {Name = pair.File.FileName.ToString(), Size = pair.File.Size, Hash = hash.Value});
            })).NotNull();

            await _quickSync.Notify<ArchiveDownloader>();
            return Ok(hashed);
        }

        [Authorize(Roles = "User")]
        [HttpGet("{game}/{version}")]
        public async Task<IActionResult> GetFiles(string game, string version)
        {
            if (!GameRegistry.TryGetByFuzzyName(game, out var meta))
                return NotFound($"Game {game} not found");
            
            var files = await _sql.GetGameFiles(meta.Game, version);
            return Ok(files.ToJson());
        }

        [Authorize(Roles = "User")]
        [HttpGet]
        public async Task<IActionResult> GetAllGames()
        {
            var registeredGames = await _sql.GetAllRegisteredGames();
            return Ok(registeredGames.ToArray().ToJson());
        }



    }
}
