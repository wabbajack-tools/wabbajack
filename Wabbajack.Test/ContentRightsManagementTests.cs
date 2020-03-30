using System;
using System.Collections.Generic;
using System.Linq;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib;
using Wabbajack.Lib.Validation;
using Game = Wabbajack.Common.Game;
using Wabbajack.Common;
using System.Threading.Tasks;
using Xunit;

namespace Wabbajack.Test
{
    public class ContentRightsManagementTests : IDisposable
    {
        private ValidateModlist validate;
        private WorkQueue queue;
        private static string server_whitelist = @"
        
        GoogleIDs:
            - googleDEADBEEF
        
        AllowedPrefixes:
            - https://somegoodplace.com/

";


        public ContentRightsManagementTests()
        {
            queue = new WorkQueue();
            validate = new ValidateModlist();
            validate.LoadServerWhitelist(server_whitelist);
        }

        public void Dispose()
        {
            queue?.Dispose();
            
        }


        [Fact]
        public async Task TestModValidation()
        {
            var modlist = new ModList
            {
                GameType = Game.Skyrim,
                Archives = new List<Archive>
                {
                    new Archive
                    {
                        State = new NexusDownloader.State                    
                        {
                            Game = Game.Skyrim,
                            Author = "bill",
                            ModID = "42",
                            FileID = "33",
                        },
                        Hash = Hash.FromLong(42)
                    }
                },
                Directives = new List<Directive>
                {
                    new FromArchive
                    {
                        ArchiveHashPath = HashRelativePath.FromStrings(Hash.FromULong(42).ToBase64(), "foo\\bar\\baz.pex"),
                        To = (RelativePath)"foo\\bar\\baz.pex"
                    }
                }
            };
            // Error due to file downloaded from 3rd party
            modlist.GameType = Game.Skyrim;
            modlist.Archives[0] = new Archive()
            {
                State = new HTTPDownloader.State() { Url = "https://somebadplace.com" },
                Hash = Hash.FromLong(42)
            };
            var errors = await validate.Validate(modlist);
            Assert.Single(errors);

            // Ok due to file downloaded from whitelisted 3rd party
            modlist.GameType = Game.Skyrim;
            modlist.Archives[0] = new Archive
            {
                State = new HTTPDownloader.State { Url = "https://somegoodplace.com/baz.7z" },
                Hash = Hash.FromLong(42)
            };
            errors = await validate.Validate(modlist);
            Assert.Empty(errors);


            // Error due to file downloaded from bad 3rd party
            modlist.GameType = Game.Skyrim;
            modlist.Archives[0] = new Archive
            {
                State = new GoogleDriveDownloader.State { Id = "bleg"},
                Hash = Hash.FromLong(42)
            };
            errors = await validate.Validate(modlist);
            Assert.Single(errors);

            // Ok due to file downloaded from good google site
            modlist.GameType = Game.Skyrim;
            modlist.Archives[0] = new Archive
            {
                State = new GoogleDriveDownloader.State { Id = "googleDEADBEEF" },
                Hash = Hash.FromLong(42)
            };
            errors = await validate.Validate(modlist);
            Assert.Empty(errors);

        }

        [Fact]
        public async Task CanLoadFromGithub()
        {
            using (var workQueue = new WorkQueue())
            {
                await new ValidateModlist().LoadListsFromGithub();
            }
        }
    }
}
