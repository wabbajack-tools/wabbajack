using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Wabbajack.Paths.IO;
using Wabbajack.Server.DataLayer;

namespace Wabbajack.BuildServer.Controllers;

[Authorize]
[Route("/users")]
public class Users : ControllerBase
{
    private ILogger<Users> _logger;
    private readonly AppSettings _settings;
    private readonly SqlService _sql;

    public Users(ILogger<Users> logger, SqlService sql, AppSettings settings)
    {
        _settings = settings;
        _logger = logger;
        _sql = sql;
    }

    [HttpGet]
    [Route("add/{Name}")]
    public async Task<string> AddUser(string Name)
    {
        return await _sql.AddLogin(Name);
    }

    [HttpGet]
    [Route("export")]
    public async Task<string> Export()
    {
        var mainFolder = _settings.TempPath.Combine("exported_users");
        mainFolder.CreateDirectory();

        foreach (var (owner, key) in await _sql.GetAllUserKeys())
        {
            var folder = mainFolder.Combine(owner);
            folder.CreateDirectory();
            await folder.Combine(_settings.AuthorAPIKeyFile).WriteAllTextAsync(key);
        }

        return "done";
    }
}