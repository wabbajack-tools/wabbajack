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
        public Dictionary<string, Author> AuthorPermissions { get; set; } = new Dictionary<string, Author>();

        private readonly WorkQueue _queue;
        public ServerWhitelist ServerWhitelist { get; set; } = new ServerWhitelist();

        public ValidateModlist(WorkQueue workQueue)
        {
            _queue = workQueue;
        }

        public void LoadAuthorPermissionsFromString(string s)
        {
            AuthorPermissions = s.FromYaml<Dictionary<string, Author>>();
        }

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
                Utils.Log($"Loaded permissions for {ServerWhitelist.AllowedPrefixes.Count} servers and {ServerWhitelist.GoogleIDs.Count} Google Drive files");
            }

        }

        public static async Task RunValidation(WorkQueue queue, ModList modlist)
        {
            var validator = new ValidateModlist(queue);

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

        /// <summary>
        /// Takes all the permissions for a given Nexus mods and merges them down to a single permissions record
        /// the more specific record having precedence in each field.
        /// </summary>
        /// <param name="mod"></param>
        /// <returns></returns>
        public Permissions FilePermissions(NexusDownloader.State mod)
        {
            var author_permissions = AuthorPermissions.GetOrDefault(mod.Author)?.Permissions;
            var game_permissions = AuthorPermissions.GetOrDefault(mod.Author)?.Games.GetOrDefault(mod.GameName)?.Permissions;
            var mod_permissions = AuthorPermissions.GetOrDefault(mod.Author)?.Games.GetOrDefault(mod.GameName)?.Mods.GetOrDefault(mod.ModID)
                ?.Permissions;
            var file_permissions = AuthorPermissions.GetOrDefault(mod.Author)?.Games.GetOrDefault(mod.GameName)?.Mods
                .GetOrDefault(mod.ModID)?.Files.GetOrDefault(mod.FileID)?.Permissions;

            return new Permissions
            {
                CanExtractBSAs = file_permissions?.CanExtractBSAs ?? mod_permissions?.CanExtractBSAs ??
                                 game_permissions?.CanExtractBSAs ?? author_permissions?.CanExtractBSAs ?? true,
                CanModifyAssets = file_permissions?.CanModifyAssets ?? mod_permissions?.CanModifyAssets ??
                                  game_permissions?.CanModifyAssets ?? author_permissions?.CanModifyAssets ?? true,
                CanModifyESPs = file_permissions?.CanModifyESPs ?? mod_permissions?.CanModifyESPs ??
                                game_permissions?.CanModifyESPs ?? author_permissions?.CanModifyESPs ?? true,
                CanUseInOtherGames = file_permissions?.CanUseInOtherGames ?? mod_permissions?.CanUseInOtherGames ??
                                     game_permissions?.CanUseInOtherGames ?? author_permissions?.CanUseInOtherGames ?? true,
            };
        }

        public async Task<IEnumerable<string>> Validate(ModList modlist)
        {
            ConcurrentStack<string> ValidationErrors = new ConcurrentStack<string>();
            
            var nexus_mod_permissions = (await modlist.Archives
                .Where(a => a.State is NexusDownloader.State)
                .PMap(_queue, a => (a.Hash, FilePermissions((NexusDownloader.State)a.State), a)))
                .ToDictionary(a => a.Hash, a => new { permissions = a.Item2, archive = a.a });

            await modlist.Directives
                .OfType<PatchedFromArchive>()
                .PMap(_queue, p =>
                {
                    if (nexus_mod_permissions.TryGetValue(p.ArchiveHashPath[0], out var archive))
                    {
                        var ext = Path.GetExtension(p.ArchiveHashPath.Last());
                        var url = (archive.archive.State as NexusDownloader.State).URL;
                        if (Consts.AssetFileExtensions.Contains(ext) && !(archive.permissions.CanModifyAssets ?? true))
                        {
                            ValidationErrors.Push($"{p.To} from {url} is set to disallow asset modification");
                        }
                        else if (Consts.ESPFileExtensions.Contains(ext) && !(archive.permissions.CanModifyESPs ?? true))
                        {
                            ValidationErrors.Push($"{p.To} from {url} is set to disallow asset ESP modification");
                        }
                    }
                });

            await modlist.Directives
                .OfType<FromArchive>()
                .PMap(_queue, p =>
                {
                    if (nexus_mod_permissions.TryGetValue(p.ArchiveHashPath[0], out var archive))
                    {
                        var url = (archive.archive.State as NexusDownloader.State).URL;
                        if (!(archive.permissions.CanExtractBSAs ?? true) &&
                            p.ArchiveHashPath.Skip(1).ButLast().Any(a => Consts.SupportedBSAs.Contains(Path.GetExtension(a).ToLower())))
                        {
                            ValidationErrors.Push($"{p.To} from {url} is set to disallow BSA extraction");
                        }
                    }
                });

            var nexus = NexusApi.NexusApiUtils.ConvertGameName(modlist.GameType.MetaData().NexusName);

            modlist.Archives
                   .Where(a => a.State is NexusDownloader.State)
                   .Where(m => NexusApi.NexusApiUtils.ConvertGameName(((NexusDownloader.State)m.State).GameName) != nexus)
                   .Do(m =>
                   {
                       var permissions = FilePermissions((NexusDownloader.State)m.State);
                       if (!(permissions.CanUseInOtherGames ?? true))
                       {
                           ValidationErrors.Push(
                               $"The ModList is for {nexus} but {m.Name} is for game type {((NexusDownloader.State)m.State).GameName} and is not allowed to be converted to other game types");
                       }
                   });

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
