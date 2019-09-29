using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Wabbajack.Common;
using Wabbajack.Validation;
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


        [TestInitialize]
        public void TestSetup()
        {
            WorkQueue.Init((x, y, z) => { }, (min, max) => { });
            validate = new ValidateModlist();
            validate.LoadAuthorPermissionsFromString(permissions);
        }

        [TestMethod]
        public void TestRightsFallthrough()
        {
            var permissions = validate.FilePermissions(new NexusMod()
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

            permissions = validate.FilePermissions(new NexusMod()
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

            permissions = validate.FilePermissions(new NexusMod()
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

            permissions = validate.FilePermissions(new NexusMod()
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

            permissions = validate.FilePermissions(new NexusMod()
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
                    new NexusMod
                    {
                        GameName = "Skyrim",
                        Author = "bill",
                        ModID = "42",
                        FileID = "33",
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
                Patch = new byte[]{0, 1, 3},
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


            // Error due to game conversion
            modlist.GameType = Game.SkyrimSpecialEdition;
            modlist.Directives[0] = new FromArchive
            {
                ArchiveHashPath = new[] { "DEADBEEF", "foo\\bar\\baz.dds" },
            };
            errors = validate.Validate(modlist);
            Assert.AreEqual(errors.Count(), 1);

        }
    }


}
