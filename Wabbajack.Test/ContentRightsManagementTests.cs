using System;
using System.Collections.Generic;
using System.Linq;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib;
using Wabbajack.Lib.Validation;
using Game = Wabbajack.Common.Game;
using Wabbajack.Common;
using System.Threading.Tasks;
using Wabbajack.Lib.NexusApi;
using Xunit;
using Xunit.Abstractions;

namespace Wabbajack.Test
{
    public class ContentRightsManagementTests : ATestBase
    {
        private ValidateModlist validate;
        private WorkQueue queue;
        private static string server_whitelist = @"
        
        GoogleIDs:
            - googleDEADBEEF
        
        AllowedPrefixes:
            - https://somegoodplace.com/

";




        public override void Dispose()
        {
            queue?.Dispose();
            base.Dispose();
        }


        [Fact]
        public async Task TestModValidation()
        {
            var modlist = new ModList
            {
                GameType = Game.Skyrim,
                Archives = new List<Archive>
                {
                    new Archive(
                        new NexusDownloader.State
                        {
                            Game = Game.Skyrim,
                            Author = "bill",
                            ModID = 42,
                            FileID = 33,
                        })
                    {
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
            modlist.Archives[0] = new Archive(new HTTPDownloader.State("https://somebadplace.com"))
            {
                Hash = Hash.FromLong(42)
            };
            var errors = await validate.Validate(modlist);
            Assert.Single(errors);

            // Ok due to file downloaded from whitelisted 3rd party
            modlist.GameType = Game.Skyrim;
            modlist.Archives[0] = new Archive(new HTTPDownloader.State("https://somegoodplace.com/baz.7z"))
            {
                Hash = Hash.FromLong(42)
            };
            errors = await validate.Validate(modlist);
            Assert.Empty(errors);


            // Error due to file downloaded from bad 3rd party
            modlist.GameType = Game.Skyrim;
            modlist.Archives[0] = new Archive(new GoogleDriveDownloader.State("bleg"))
            {
                Hash = Hash.FromLong(42)
            };
            errors = await validate.Validate(modlist);
            Assert.Single(errors);

            // Ok due to file downloaded from good google site
            modlist.GameType = Game.Skyrim;
            modlist.Archives[0] = new Archive(new GoogleDriveDownloader.State("googleDEADBEEF"))
            {
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
        
        [Fact]
        public async Task CanGetReuploadRights()
        {
            Assert.Equal(HTMLInterface.PermissionValue.No, await HTMLInterface.GetUploadPermissions(Game.SkyrimSpecialEdition, 266));
            Assert.Equal(HTMLInterface.PermissionValue.Yes, await HTMLInterface.GetUploadPermissions(Game.SkyrimSpecialEdition, 1137));
            Assert.Equal(HTMLInterface.PermissionValue.Hidden, await HTMLInterface.GetUploadPermissions(Game.SkyrimSpecialEdition, 34604));
            Assert.Equal(HTMLInterface.PermissionValue.NotFound, await HTMLInterface.GetUploadPermissions(Game.SkyrimSpecialEdition, 24287));
            
        }

        public ContentRightsManagementTests(ITestOutputHelper output) : base(output)
        {
            queue = new WorkQueue();
            validate = new ValidateModlist();
            validate.LoadServerWhitelist(server_whitelist);
        }
    }
}
