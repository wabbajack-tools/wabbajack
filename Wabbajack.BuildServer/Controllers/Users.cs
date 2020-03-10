using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Wabbajack.BuildServer.Model.Models;
using Wabbajack.BuildServer.Models;
using Wabbajack.Common;

namespace Wabbajack.BuildServer.Controllers
{
    [Authorize]
    [Route("/users")]
    public class Users : AControllerBase<Users>
    {
        public Users(ILogger<Users> logger, DBContext db, SqlService sql) : base(logger, db, sql)
        {
        }
        
        [HttpGet]
        [Route("add/{Name}")]
        public async Task<string> AddUser(string Name)
        {
            var user = new ApiKey();
            var arr = new byte[128];
            new Random().NextBytes(arr);
            user.Owner = Name;
            user.Key = arr.ToHex();
            user.Id = Guid.NewGuid().ToString();
            user.Roles = new List<string>();
            user.CanUploadLists = new List<string>();

            await Db.ApiKeys.InsertOneAsync(user);
            
            return user.Id;
        }

        [HttpGet]
        [Route("export")]
        public async Task<string> Export()
        {
            if (!Directory.Exists("exported_users"))
                Directory.CreateDirectory("exported_users");

            foreach (var user in await Db.ApiKeys.AsQueryable().ToListAsync())
            {
                Directory.CreateDirectory(Path.Combine("exported_users", user.Owner));
                Alphaleonis.Win32.Filesystem.File.WriteAllText(Path.Combine("exported_users", user.Owner, "author-api-key.txt"), user.Key);
            }

            return "done";
        }

    }
    
}
