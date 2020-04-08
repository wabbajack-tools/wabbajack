using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Lib.Downloaders;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace Wabbajack.Lib.Validation
{
    /// <summary>
    /// Core class for rights management. Given a Wabbajack ModList this class will return a list of all the
    /// known rights violations of the ModList
    /// </summary>
    public class ValidateModlist
    {
        public ServerWhitelist ServerWhitelist { get; private set; } = new ServerWhitelist();
        public void LoadServerWhitelist(string s)
        {
            ServerWhitelist = s.FromYaml<ServerWhitelist>();
        }

        public async Task LoadListsFromGithub()
        {
            var client = new Common.Http.Client();

            Utils.Log("Loading server whitelist");
            using (var response = await client.GetAsync(Consts.ServerWhitelistURL))
            using (var result = await response.Content.ReadAsStreamAsync())
            {
                ServerWhitelist = result.FromYaml<ServerWhitelist>();
                Utils.Log($"Loaded permissions for {ServerWhitelist.AllowedPrefixes?.Count ?? 0} servers and {ServerWhitelist.GoogleIDs?.Count ?? 0} Google Drive files");
            }

        }

        public static async Task RunValidation(ModList modlist)
        {
            var validator = new ValidateModlist();

            await validator.LoadListsFromGithub();

            Utils.Log("Running validation checks");
            var errors = await validator.Validate(modlist);
            errors.Do(e => Utils.Log(e));
            if (errors.Count() > 0)
            {
                throw new Exception($"{errors.Count()} validation errors found, cannot continue.");
            }
            else
            {
                Utils.Log("No validation failures");
            }
        }

        public async Task<IEnumerable<string>> Validate(ModList modlist)
        {
            ConcurrentStack<string> ValidationErrors = new ConcurrentStack<string>();
            modlist.Archives
                .Where(m => !m.State.IsWhitelisted(ServerWhitelist))
                .Do(m =>
                {
                    ValidationErrors.Push($"{m.Name} is not a whitelisted download");
                });
                
            return ValidationErrors.ToList();
        }
    }
}
