using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib;
using Wabbajack.Lib.Validation;
using Game = Wabbajack.Common.Game;

namespace Wabbajack.Test
{
    [TestClass]
    public class ContentRightsManagementTests
    {
        private ValidateModlist validate;

        private static string permissions = @"

        bill: 
            Permissions:
                CanExtractBSAs: false
            Games:
                Skyrim:
                    Permissions:
                        CanModifyESPs: false
                    Mods:
                        42:
                            Permissions:
                                CanModifyAssets: false
                            Files:
                                33:
                                    Permissions:
                                        CanUseInOtherGames: false
";

        private static string server_whitelist = @"
        
        GoogleIDs:
            - googleDEADBEEF
        
        AllowedPrefixes:
            - https://somegoodplace.com/

";


        [TestInitialize]
        public void TestSetup()
        {
            validate = new ValidateModlist();
            validate.LoadAuthorPermissionsFromString(permissions);
            validate.LoadServerWhitelist(server_whitelist);
        }

        [TestMethod]
        public void TestRightsFallthrough()
        {
            var permissions = validate.FilePermissions(new NexusDownloader.State
            {
                Author = "bill",
                GameName = "Skyrim",
                ModID = "42",
                FileID = "33"
            });

            permissions.CanExtractBSAs.AssertIsFalse();
            permissions.CanModifyESPs.AssertIsFalse();
            permissions.CanModifyAssets.AssertIsFalse();
            permissions.CanUseInOtherGames.AssertIsFalse();

            permissions = validate.FilePermissions(new NexusDownloader.State
            {
                Author = "bob",
                GameName = "Skyrim",
                ModID = "42",
                FileID = "33"
            });

            permissions.CanExtractBSAs.AssertIsTrue();
            permissions.CanModifyESPs.AssertIsTrue();
            permissions.CanModifyAssets.AssertIsTrue();
            permissions.CanUseInOtherGames.AssertIsTrue();

            permissions = validate.FilePermissions(new NexusDownloader.State
            {
                Author = "bill",
                GameName = "Fallout4",
                ModID = "42",
                FileID = "33"
            });

            permissions.CanExtractBSAs.AssertIsFalse();
            permissions.CanModifyESPs.AssertIsTrue();
            permissions.CanModifyAssets.AssertIsTrue();
            permissions.CanUseInOtherGames.AssertIsTrue();

            permissions = validate.FilePermissions(new NexusDownloader.State
            {
                Author = "bill",
                GameName = "Skyrim",
                ModID = "43",
                FileID = "33"
            });

            permissions.CanExtractBSAs.AssertIsFalse();
            permissions.CanModifyESPs.AssertIsFalse();
            permissions.CanModifyAssets.AssertIsTrue();
            permissions.CanUseInOtherGames.AssertIsTrue();

            permissions = validate.FilePermissions(new NexusDownloader.State
            {
                Author = "bill",
                GameName = "Skyrim",
                ModID = "42",
                FileID = "31"
            });

            permissions.CanExtractBSAs.AssertIsFalse();
            permissions.CanModifyESPs.AssertIsFalse();
            permissions.CanModifyAssets.AssertIsFalse();
            permissions.CanUseInOtherGames.AssertIsTrue();
        }


        [TestMethod]
        public void TestModValidation()
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
                            GameName = "Skyrim",
                            Author = "bill",
                            ModID = "42",
                            FileID = "33",
                        },
                        Hash = "DEADBEEF"
                    }

                },
                Directives = new List<Directive>
                {
                    new FromArchive
                    {
                        ArchiveHashPath = new[] {"DEADBEEF", "foo\\bar\\baz.pex"},
                        To = "foo\\bar\\baz.pex"
                    }
                }
            };

            IEnumerable<string> errors;

            // No errors, simple archive extraction
            errors = validate.Validate(modlist);
            Assert.AreEqual(errors.Count(), 0);


            // Error due to patched file
            modlist.Directives[0] = new PatchedFromArchive
            {
                PatchID = Guid.NewGuid().ToString(),
                ArchiveHashPath = new[] {"DEADBEEF", "foo\\bar\\baz.pex"},
            };

            errors = validate.Validate(modlist);
            Assert.AreEqual(errors.Count(), 1);

            // Error due to extracted BSA file
            modlist.Directives[0] = new FromArchive
            {
                ArchiveHashPath = new[] { "DEADBEEF", "foo.bsa", "foo\\bar\\baz.dds" },
            };

            errors = validate.Validate(modlist);
            Assert.AreEqual(errors.Count(), 1);

            // No error since we're just installing the .bsa, not extracting it
            modlist.Directives[0] = new FromArchive
            {
                ArchiveHashPath = new[] { "DEADBEEF", "foo.bsa"},
            };

            errors = validate.Validate(modlist);
            Assert.AreEqual(0, errors.Count());
            
            // Error due to game conversion
            modlist.GameType = Game.SkyrimSpecialEdition;
            modlist.Directives[0] = new FromArchive
            {
                ArchiveHashPath = new[] { "DEADBEEF", "foo\\bar\\baz.dds" },
            };
            errors = validate.Validate(modlist);
            Assert.AreEqual(errors.Count(), 1);

            // Error due to file downloaded from 3rd party
            modlist.GameType = Game.Skyrim;
            modlist.Archives[0] = new Archive()
            {
                State = new HTTPDownloader.State() { Url = "https://somebadplace.com" },
                Hash = "DEADBEEF"
            };
            errors = validate.Validate(modlist);
            Assert.AreEqual(1, errors.Count());

            // Ok due to file downloaded from whitelisted 3rd party
            modlist.GameType = Game.Skyrim;
            modlist.Archives[0] = new Archive
            {
                State = new HTTPDownloader.State { Url = "https://somegoodplace.com/baz.7z" },
                Hash = "DEADBEEF"
            };
            errors = validate.Validate(modlist);
            Assert.AreEqual(0, errors.Count());


            // Error due to file downloaded from bad 3rd party
            modlist.GameType = Game.Skyrim;
            modlist.Archives[0] = new Archive
            {
                State = new GoogleDriveDownloader.State { Id = "bleg"},
                Hash = "DEADBEEF"
            };
            errors = validate.Validate(modlist);
            Assert.AreEqual(errors.Count(), 1);

            // Ok due to file downloaded from good google site
            modlist.GameType = Game.Skyrim;
            modlist.Archives[0] = new Archive
            {
                State = new GoogleDriveDownloader.State { Id = "googleDEADBEEF" },
                Hash = "DEADBEEF"
            };
            errors = validate.Validate(modlist);
            Assert.AreEqual(0, errors.Count());

        }

        [TestMethod]
        public void CanLoadFromGithub()
        {
            new ValidateModlist().LoadListsFromGithub();
        }
    }


}
