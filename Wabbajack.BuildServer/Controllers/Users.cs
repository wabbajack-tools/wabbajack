using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Wabbajack.BuildServer.Model.Models;
using Wabbajack.Common;

namespace Wabbajack.BuildServer.Controllers
{
    [Authorize]
    [Route("/users")]
    public class Users : AControllerBase<Users>
    {
        private AppSettings _settings;

        public Users(ILogger<Users> logger, SqlService sql, AppSettings settings) : base(logger, sql)
        {
            _settings = settings;
        }
        
        [HttpGet]
        [Route("add/{Name}")]
        public async Task<string> AddUser(string Name)
        {
            return await SQL.AddLogin(Name);
        }

        [HttpGet]
        [Route("export")]
        public async Task<string> Export()
        {
            var mainFolder = _settings.TempPath.Combine("exported_users");
            mainFolder.CreateDirectory();

            foreach (var (owner, key) in await SQL.GetAllUserKeys())
            {
                var folder = mainFolder.Combine(owner);
                folder.CreateDirectory();
                await folder.Combine(Consts.AuthorAPIKeyFile).WriteAllTextAsync(key);
            }

            return "done";
        }

    }
    
}
