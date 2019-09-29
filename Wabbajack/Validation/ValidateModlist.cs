using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Wabbajack.Common;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Wabbajack.Validation
{
    /// <summary>
    /// Core class for rights management. Given a Wabbajack modlist this class will return a list of all the
    /// know rights violations of the modlist
    /// </summary>
    public class ValidateModlist
    {
        public Dictionary<string, Author> AuthorPermissions { get; set; }

        public void LoadAuthorPermissionsFromString(string s)
        {
            var d = new DeserializerBuilder()
                .WithNamingConvention(new PascalCaseNamingConvention())
                .Build();
            AuthorPermissions = d.Deserialize<Dictionary<string, Author>>(s);
        }

        /// <summary>
        /// Takes all the permissions for a given Nexus mods and merges them down to a single permissions record
        /// the more specific record having precedence in each field.
        /// </summary>
        /// <param name="mod"></param>
        /// <returns></returns>
        public Permissions FilePermissions(NexusMod mod)
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

        public IEnumerable<string> Validate(ModList modlist)
        {
            ConcurrentStack<string> ValidationErrors = new ConcurrentStack<string>();

            var nexus_mod_permissions = modlist.Archives
                .OfType<NexusMod>()
                .PMap(a => (a.Hash, FilePermissions(a), a))
                .ToDictionary(a => a.Hash, a => new { permissions = a.Item2, archive = a.a});

            modlist.Directives
                .OfType<PatchedFromArchive>()
                .PMap(p =>
                {
                    if (nexus_mod_permissions.TryGetValue(p.ArchiveHashPath[0], out var archive))
                    {
                        var ext = Path.GetExtension(p.ArchiveHashPath.Last());
                        if (Consts.AssetFileExtensions.Contains(ext) && !(archive.permissions.CanModifyAssets ?? true))
                        {
                            ValidationErrors.Push($"{p.To} from {archive.archive.NexusURL} is set to disallow asset modification");
                        }
                        else if (Consts.ESPFileExtensions.Contains(ext) && !(archive.permissions.CanModifyESPs ?? true))
                        {
                            ValidationErrors.Push($"{p.To} from {archive.archive.NexusURL} is set to disallow asset ESP modification");
                        }
                    }
                });

            modlist.Directives
                .OfType<FromArchive>()
                .PMap(p =>
                {
                    if (nexus_mod_permissions.TryGetValue(p.ArchiveHashPath[0], out var archive))
                    {
                        if (!(archive.permissions.CanExtractBSAs ?? true) && 
                            p.ArchiveHashPath.Skip(1).Any(a => Consts.SupportedBSAs.Contains(Path.GetExtension(a))))
                        {
                            ValidationErrors.Push($"{p.To} from {archive.archive.NexusURL} is set to disallow BSA Extraction");
                        }
                    }
                });

            var nexus = NexusApi.NexusApiUtils.ConvertGameName(GameRegistry.Games[modlist.GameType].NexusName);

            modlist.Archives
                   .OfType<NexusMod>()
                   .Where(m => m.GameName.ToLower() != nexus)
                   .Do(m => ValidationErrors.Push($"The modlist is for {nexus} but {m.Name} is for game type {m.GameName} and is not allowed to be converted to other game types"));


            return ValidationErrors.ToList();
        }
    }
}
