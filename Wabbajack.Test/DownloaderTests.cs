using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using Wabbajack.Common;
using Wabbajack.Common.StatusFeed;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.Http;
using Wabbajack.Lib.LibCefHelpers;
using Wabbajack.Lib.NexusApi;
using Wabbajack.Lib.Validation;
using Wabbajack.Lib.WebAutomation;
using Xunit;
using Xunit.Abstractions;
using Directory = System.IO.Directory;
using File = Alphaleonis.Win32.Filesystem.File;
using Game = Wabbajack.Common.Game;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace Wabbajack.Test
{
    public class DownloaderTests : XunitContextBase, IDisposable
    {
        private IDisposable _unsubMsgs;
        private IDisposable _unsubErr;

        public DownloaderTests(ITestOutputHelper helper) : base(helper)
        {
            Helpers.Init(); 
            _unsubMsgs = Utils.LogMessages.OfType<IInfo>().Subscribe(onNext: msg => XunitContext.WriteLine(msg.ShortDescription));
            _unsubErr = Utils.LogMessages.OfType<IUserIntervention>().Subscribe(msg =>
                XunitContext.WriteLine("ERROR: User intervention required: " + msg.ShortDescription));
        }

        public override void Dispose()
        {
            base.Dispose();
            _unsubErr.Dispose();
            _unsubMsgs.Dispose();
        }

        
        /*
        [Fact]
        public async Task TestAllPrepares()
        {
            foreach (var downloader in DownloadDispatcher.Downloaders)
                await downloader.Prepare();
        }*/

        [Fact]
        public async Task MegaDownload()
        {
            var ini = @"[General]
                        directURL=https://mega.nz/#!CsMSFaaJ!-uziC4mbJPRy2e4pPk8Gjb3oDT_38Be9fzZ6Ld4NL-k";

            var state = (AbstractDownloadState)await DownloadDispatcher.ResolveArchive(ini.LoadIniString());

            Assert.NotNull(state);

            var url_state = DownloadDispatcher.ResolveArchive(
                "https://mega.nz/#!CsMSFaaJ!-uziC4mbJPRy2e4pPk8Gjb3oDT_38Be9fzZ6Ld4NL-k");

            Assert.Equal("https://mega.nz/#!CsMSFaaJ!-uziC4mbJPRy2e4pPk8Gjb3oDT_38Be9fzZ6Ld4NL-k",
                ((MegaDownloader.State)url_state).Url);


            var converted = RoundTripState(state);
            Assert.True(await converted.Verify(new Archive(state: null!){Size = 20}));
            await using var filename = new TempFile();

            Assert.True(converted.IsWhitelisted(new ServerWhitelist {AllowedPrefixes = new List<string>{"https://mega.nz/#!CsMSFaaJ!-uziC4mbJPRy2e4pPk8Gjb3oDT_38Be9fzZ6Ld4NL-k" } }));
            Assert.False(converted.IsWhitelisted(new ServerWhitelist { AllowedPrefixes = new List<string>{ "blerg" }}));

            await DownloadDispatcher.DownloadWithPossibleUpgrade(new Archive(state: converted) {Name = "MEGA Test.txt"}, filename.Path);

            Assert.Equal(Hash.FromBase64("eSIyd+KOG3s="), await filename.Path.FileHashAsync());

            Assert.Equal("Cheese for Everyone!", await filename.Path.ReadAllTextAsync());
        }

        [Fact]
        public async Task DropboxTests()
        {
            var ini = @"[General]
                        directURL=https://www.dropbox.com/s/5hov3m2pboppoc2/WABBAJACK_TEST_FILE.txt?dl=0";

            var state = (AbstractDownloadState)await DownloadDispatcher.ResolveArchive(ini.LoadIniString());

            Assert.NotNull(state);

            var url_state = DownloadDispatcher.ResolveArchive(
                "https://www.dropbox.com/s/5hov3m2pboppoc2/WABBAJACK_TEST_FILE.txt?dl=0");

            Assert.Equal("https://www.dropbox.com/s/5hov3m2pboppoc2/WABBAJACK_TEST_FILE.txt?dl=1", 
                ((HTTPDownloader.State)url_state).Url);

            var converted = RoundTripState(state);
            Assert.True(await converted.Verify(new Archive(state: null!){Size = 20}));
            await using var filename = new TempFile();

            Assert.True(converted.IsWhitelisted(new ServerWhitelist { AllowedPrefixes = new List<string> { "https://www.dropbox.com/s/5hov3m2pboppoc2/WABBAJACK_TEST_FILE.txt?" } }));
            Assert.False(converted.IsWhitelisted(new ServerWhitelist { AllowedPrefixes = new List<string> { "blerg" } }));

            await converted.Download(new Archive(state: null!) { Name = "MEGA Test.txt" }, filename.Path);

            Assert.Equal(Hash.FromBase64("eSIyd+KOG3s="), await filename.Path.FileHashAsync());

            Assert.Equal("Cheese for Everyone!", await filename.Path.ReadAllTextAsync());

            var newState = (AbstractDownloadState)new GoogleDriveDownloader.State("1Q_CdeYJStfoTZFLZ79RRVkxI2c_cG0dg");
            Assert.True(await newState.Verify(new Archive(newState) {Size = 0}));
        }

        [Fact]
        public async Task GoogleDriveTests()
        {
            var ini = @"[General]
                        directURL=https://drive.google.com/file/d/1grLRTrpHxlg7VPxATTFNfq2OkU_Plvh_/view?usp=sharing";

            var state = (AbstractDownloadState)await DownloadDispatcher.ResolveArchive(ini.LoadIniString());

            Assert.NotNull(state);

            var url_state = DownloadDispatcher.ResolveArchive(
                "https://drive.google.com/file/d/1grLRTrpHxlg7VPxATTFNfq2OkU_Plvh_/view?usp=sharing");

            Assert.Equal("1grLRTrpHxlg7VPxATTFNfq2OkU_Plvh_",
                ((GoogleDriveDownloader.State)url_state).Id);

            var converted = RoundTripState(state);
            Assert.True(await converted.Verify(new Archive(state: null!) { Size = 20}));
            await using var filename = new TempFile();

            Assert.True(converted.IsWhitelisted(new ServerWhitelist { GoogleIDs = new List<string> { "1grLRTrpHxlg7VPxATTFNfq2OkU_Plvh_" } }));
            Assert.False(converted.IsWhitelisted(new ServerWhitelist { GoogleIDs = new List<string>()}));

            await DownloadDispatcher.DownloadWithPossibleUpgrade(new Archive(converted) { Name = "MEGA Test.txt" }, filename.Path);

            Assert.Equal(Hash.FromBase64("eSIyd+KOG3s="), await filename.Path.FileHashAsync());

            Assert.Equal("Cheese for Everyone!", await filename.Path.ReadAllTextAsync());
        }

        [Fact]
        public async Task HttpDownload()
        {
            var ini = @"[General]
                        directURL=http://build.wabbajack.org/WABBAJACK_TEST_FILE.txt";

            var state = (AbstractDownloadState)await DownloadDispatcher.ResolveArchive(ini.LoadIniString());

            Assert.NotNull(state);

            var url_state = DownloadDispatcher.ResolveArchive("http://build.wabbajack.org/WABBAJACK_TEST_FILE.txt");

            Assert.Equal("http://build.wabbajack.org/WABBAJACK_TEST_FILE.txt",
                ((HTTPDownloader.State)url_state).Url);

            var converted = RoundTripState(state);
            Assert.True(await converted.Verify(new Archive(state: null!) { Size = 20}));
            await using var filename = new TempFile();

            Assert.True(converted.IsWhitelisted(new ServerWhitelist { AllowedPrefixes = new List<string> { "http://build.wabbajack.org/" } }));
            Assert.False(converted.IsWhitelisted(new ServerWhitelist { AllowedPrefixes = new List<string>() }));

            await converted.Download(new Archive(state: null!) { Name = "MEGA Test.txt" }, filename.Path);

            Assert.Equal(Hash.FromBase64("eSIyd+KOG3s="), await filename.Path.FileHashAsync());

            Assert.Equal("Cheese for Everyone!", await filename.Path.ReadAllTextAsync());
        }

        [Fact]
        public async Task ManualDownload()
        {
            var ini = @"[General]
                        manualURL=http://build.wabbajack.org/WABBAJACK_TEST_FILE.zip";

            var state = (AbstractDownloadState)await DownloadDispatcher.ResolveArchive(ini.LoadIniString());

            Assert.NotNull(state);

            var converted = RoundTripState(state);
            Assert.True(await converted.Verify(new Archive(state: null!) { Size = 20}));
            await using var filename = new TempFile();

            Assert.True(converted.IsWhitelisted(new ServerWhitelist { AllowedPrefixes = new List<string> { "http://build.wabbajack.org/" } }));

            
            // Doesn't work well on the test server, so we're disabling for now
            //await converted.Download(new Archive { Name = "WABBAJACK_TEST_FILE.zip", Size = 20, Hash = "eSIyd+KOG3s="}, filename.Path);
            //Assert.Equal("eSIyd+KOG3s=", Utils.FileHash(filename.Path));
            //Assert.Equal(File.ReadAllText(filename.Path), "Cheese for Everyone!");
        }

        
        [Fact]
        public async Task MediaFireDownload()
        {
            var ini = @"[General]
                    directURL=http://www.mediafire.com/file/agiqzm1xwebczpx/WABBAJACK_TEST_FILE.txt";

            var state = (AbstractDownloadState)await DownloadDispatcher.ResolveArchive(ini.LoadIniString());

            Assert.NotNull(state);

            var url_state = DownloadDispatcher.ResolveArchive(
                "http://www.mediafire.com/file/agiqzm1xwebczpx/WABBAJACK_TEST_FILE.txt");

            Assert.Equal("http://www.mediafire.com/file/agiqzm1xwebczpx/WABBAJACK_TEST_FILE.txt",
                ((MediaFireDownloader.State) url_state).Url);

            var converted = RoundTripState(state);
            Assert.True(await converted.Verify(new Archive(state: null!) { Size = 20 }));
            await using var filename = new TempFile();

            Assert.True(converted.IsWhitelisted(new ServerWhitelist
                {AllowedPrefixes = new List<string> {"http://www.mediafire.com/file/agiqzm1xwebczpx/"}}));
            Assert.False(converted.IsWhitelisted(new ServerWhitelist {AllowedPrefixes = new List<string>()}));

            await DownloadDispatcher.DownloadWithPossibleUpgrade(new Archive(state: converted) { Name = "Media Fire Test.zip" }, filename.Path);

            Assert.Equal("Cheese for Everyone!", await filename.Path.ReadAllTextAsync());

        }

        [Fact]
        public async Task NexusDownload()
        {
            var ini = @"[General]
                        gameName=SkyrimSE
                        modID = 12604
                        fileID=35407";

            var state = (AbstractDownloadState)await DownloadDispatcher.ResolveArchive(ini.LoadIniString());
            await DownloadDispatcher.PrepareAll(new[] {state});

            Assert.NotNull(state);


            var converted = RoundTripState(state);
            Assert.True(await converted.Verify(new Archive(state: null!) { Size = 20 }));
            // Exercise the cache code
            Assert.True(await converted.Verify(new Archive(state: null!) { Size = 20 }));
            await using var filename = new TempFile();

            Assert.True(converted.IsWhitelisted(new ServerWhitelist { AllowedPrefixes = new List<string>() }));

            await converted.Download(new Archive(state: null!) { Name = "SkyUI.7z" }, filename.Path);

            Assert.Equal(Hash.FromBase64("dF2yafV2Oks="), await filename.Path.FileHashAsync());

            // Verify that we can see a older file
            var data = await (await NexusApiClient.Get()).GetModFile(Game.SkyrimSpecialEdition, 45221, 185392, useCache:false);
            Assert.Equal("Smooth Combat - Non Combat Animation System 2.3", data.name);
            
        }

        
        // [Fact] - Disabled while Moddb is dead
        public async Task ModDbTests()
        {
            var ini = @"[General]
                        directURL=https://www.moddb.com/downloads/start/124908?referer=https%3A%2F%2Fwww.moddb.com%2Fmods%2Fautopause";

            var state = (AbstractDownloadState)await DownloadDispatcher.ResolveArchive(ini.LoadIniString());

            Assert.NotNull(state);

            var url_state = DownloadDispatcher.ResolveArchive(
                "https://www.moddb.com/downloads/start/124908?referer=https%3A%2F%2Fwww.moddb.com%2Fmods%2Fautopause");

            Assert.Equal("https://www.moddb.com/downloads/start/124908?referer=https%3A%2F%2Fwww.moddb.com%2Fmods%2Fautopause",
                ((ModDBDownloader.State)url_state).Url);

            var converted = RoundTripState(state);
            Assert.True(await converted.Verify(new Archive(state: null!) { Size = 20}));
            await using var filename = new TempFile();

            Assert.True(converted.IsWhitelisted(new ServerWhitelist { AllowedPrefixes = new List<string>() }));

            await converted.Download(new Archive(state: null!) { Name = "moddbtest.7z" }, filename.Path);

            Assert.Equal(Hash.FromBase64("2lZt+1h6wxM="), await filename.Path.FileHashAsync());
        }


        [Fact]
        public async Task CanFindOtherLLMods()
        {
            var downloader = DownloadDispatcher.GetInstance<LoversLabOAuthDownloader>();
            await downloader.Prepare();
           
            var ini = @"[General]
                    ips4Site=Lovers Lab
                    ips4Mod=11116
                    ips4File=ffooo.zip";

            var state = (AbstractDownloadState)await DownloadDispatcher.ResolveArchive(ini.LoadIniString());
            var otherfiles = await ((LoversLabOAuthDownloader.State)state).GetFilesInGroup();
            
            // Throws a NPE
            var data = await downloader.GetDownloads(9023);
        }
        

        [Fact]
        public async Task CanGetLLMetadata()
        {
            await DownloadDispatcher.GetInstance<LoversLabOAuthDownloader>().Prepare();
            var ini = @"[General]
                        ips4Site=Lovers Lab
                        ips4Mod=11116
                        ips4File=WABBAJACK_TEST_FILE.zip";

            var state = (LoversLabOAuthDownloader.State)await DownloadDispatcher.ResolveArchive(ini.LoadIniString());
            Assert.True(await state.LoadMetaData());
            Assert.Equal("halgari", state.Author);
        }
        
        [Fact]
        public async Task LoversLabDownload()
        {

            
            await DownloadDispatcher.GetInstance<LoversLabOAuthDownloader>().Prepare();
            var ini = @"[General]
                        ips4Site=Lovers Lab
                        ips4Mod=11116
                        ips4File=WABBAJACK_TEST_FILE.zip";

            var state = (AbstractDownloadState)await DownloadDispatcher.ResolveArchive(ini.LoadIniString());

            Assert.NotNull(state);

            var converted = RoundTripState(state);
            Assert.True(await converted.Verify(new Archive(state: null!) { Size = 20}));

            // Verify with different Size
            Assert.False(await converted.Verify(new Archive(state: null!) { Size = 15}));

            
            await using var filename = new TempFile();
            Assert.True(converted.IsWhitelisted(new ServerWhitelist { AllowedPrefixes = new List<string>() }));

            await converted.Download(new Archive(state: null!) { Name = "LoversLab Test.txt" }, filename.Path);

            Assert.Equal(Hash.FromBase64("eSIyd+KOG3s="), await filename.Path.FileHashAsync());

            Assert.Equal("Cheese for Everyone!", await filename.Path.ReadAllTextAsync());

        }
        
        [Fact]
        public async Task LoversLabAttachmentDownload()
        {

            
            await DownloadDispatcher.GetInstance<LoversLabOAuthDownloader>().Prepare();
            var ini = @"[General]
                        ips4Site=Lovers Lab
                        ips4Attachment=853295";

            var state = (AbstractDownloadState)await DownloadDispatcher.ResolveArchive(ini.LoadIniString());

            Assert.NotNull(state);

            var converted = RoundTripState(state);
            Assert.True(await converted.Verify(new Archive(state: null!) { Size = 1363396}));

            // Verify with different Size
            Assert.False(await converted.Verify(new Archive(state: null!) { Size = 15}));

            
            await using var filename = new TempFile();
            Assert.True(converted.IsWhitelisted(new ServerWhitelist { AllowedPrefixes = new List<string>() }));

            await converted.Download(new Archive(state: null!) { Name = "LoversLab Test.txt" }, filename.Path);

            Assert.Equal(Hash.FromBase64("gLJDxGDaeQ0="), await filename.Path.FileHashAsync());
            
            Assert.False(await ((LoversLabOAuthDownloader.State) state).LoadMetaData());
            


        }

        [Fact]
        public async Task CanLoadOldLLMeta()
        {
            var state = (DeprecatedLoversLabDownloader.State)(AbsolutePath.EntryPoint.Combine(@"Resources\LoversLabState.json").FromJson<Archive>().State);
            Assert.Equal("DeprecatedLoversLabDownloader+State|", state.PrimaryKeyString);
        }
        
        [Fact]
        public async Task VectorPlexusDownload()
        {
            await DownloadDispatcher.GetInstance<VectorPlexusOAuthDownloader>().Prepare();
            var ini = @"[General]
                        ips4Site=Vector Plexus
                        ips4Mod=290
                        ips4File=WABBAJACK_TEST_FILE.zip";

            var state = (AbstractDownloadState)await DownloadDispatcher.ResolveArchive(ini.LoadIniString());

            Assert.NotNull(state);

            var converted = RoundTripState(state);
            Assert.True(await converted.Verify(new Archive(state: null!) { Size = 20}));
            await using var filename = new TempFile();

            Assert.True(converted.IsWhitelisted(new ServerWhitelist { AllowedPrefixes = new List<string>() }));

            var archive = new Archive(state: null!) {Name = "Vector Plexus Test.zip"};
            await converted.Download(archive, filename.Path);

            Assert.Equal(Hash.FromBase64("eSIyd+KOG3s="), await filename.Path.FileHashAsync());

            Assert.Equal("Cheese for Everyone!", await filename.Path.ReadAllTextAsync());
            Assert.True(converted is VectorPlexusOAuthDownloader.State);
            
            var st = (VectorPlexusOAuthDownloader.State)converted;
            Assert.True(await st.LoadMetaData());
            Assert.Equal("halgari", st.Author);
            Assert.Equal("Wabbajack Test File", st.Name);
            Assert.True(st.IsNSFW);
            Assert.Equal("1.0.0", st.Version);
            Assert.Equal("https://vectorplexus.com/files/file/290-wabbajack-test-file/", st.GetManifestURL(archive));
            Assert.True(st.ImageURL != null);
            
            
        }
        
        [Fact]
        public async Task YandexDownloader()
        {
            await DownloadDispatcher.GetInstance<YandexDownloader>().Prepare();
            var ini = @"[General]
                        directURL=https://yadi.sk/d/jqwQT4ByYtC9Tw";

            var state = (AbstractDownloadState)await DownloadDispatcher.ResolveArchive(ini.LoadIniString());

            Assert.NotNull(state);

            var converted = RoundTripState(state);
            Assert.True(await converted.Verify(new Archive(state: null!) { Size = 20}));
            await using var filename = new TempFile();

            Assert.True(converted.IsWhitelisted(new ServerWhitelist { AllowedPrefixes = new List<string>() {"https://yadi.sk"} }));

            await converted.Download(new Archive(state: null!) { Name = "WABBAJACK_TEST_FILE.txt" }, filename.Path);

            Assert.Equal(Hash.FromBase64("eSIyd+KOG3s="), await filename.Path.FileHashAsync());

            Assert.Equal("Cheese for Everyone!", await filename.Path.ReadAllTextAsync());
        }


        [Fact]
        public async Task TESAllianceDownload()
        {
            await DownloadDispatcher.GetInstance<TESAllianceDownloader>().Prepare();
            const string ini = "[General]\n" +
                               "directURL=http://tesalliance.org/forums/index.php?/files/file/2035-wabbajack-test-file/";

            var state = (AbstractDownloadState)await DownloadDispatcher.ResolveArchive(ini.LoadIniString());

            Assert.NotNull(state);

            var converted = RoundTripState(state);
            Assert.True(await converted.Verify(new Archive(state: null!) { Size = 20}));
            await using var filename = new TempFile();

            Assert.True(converted.IsWhitelisted(new ServerWhitelist { AllowedPrefixes = new List<string>() }));

            await converted.Download(new Archive(state: null!) { Name = "TESAlliance Test.zip" }, filename.Path);

            Assert.Equal(Hash.FromBase64("eSIyd+KOG3s="), await filename.Path.FileHashAsync());

            Assert.Equal("Cheese for Everyone!", await filename.Path.ReadAllTextAsync());
        }
  
        /* Site is broken again
        [Fact]
        public async Task TESAllDownloader()
        {
            await DownloadDispatcher.GetInstance<TESAllDownloader>().Prepare();
            const string ini = "[General]\n" +
                               "directURL=https://tesall.ru/files/download/594545";

            var state = (AbstractDownloadState)await DownloadDispatcher.ResolveArchive(ini.LoadIniString());

            Assert.NotNull(state);

            var converted = RoundTripState(state);
            Assert.True(await converted.Verify(new Archive(state: null!) { Size = 20}));
            await using var filename = new TempFile();

            Assert.True(converted.IsWhitelisted(new ServerWhitelist { AllowedPrefixes = new List<string>() }));

            await converted.Download(new Archive(state: null!) { Name = "TESAll Test.zip" }, filename.Path);

            Assert.Equal(Hash.FromBase64("eSIyd+KOG3s="), await filename.Path.FileHashAsync());

            Assert.Equal("Cheese for Everyone!", await filename.Path.ReadAllTextAsync());
        }*/

        /* WAITING FOR APPROVAL BY MODERATOR
         [Fact]
        public async Task DeadlyStreamDownloader()
        {
            await DownloadDispatcher.GetInstance<DeadlyStreamDownloader>().Prepare();
            const string ini = "[General]\n" +
                               "directURL=https://deadlystream.com/files/file/1550-wabbajack-test-file/";

            var state = (AbstractDownloadState)await DownloadDispatcher.ResolveArchive(ini.LoadIniString());

            Assert.NotNull(state);

            var converted = RoundTripState(state);
            Assert.True(await converted.Verify(new Archive{Size = 20}));
            using var filename = new TempFile();

            Assert.True(converted.IsWhitelisted(new ServerWhitelist { AllowedPrefixes = new List<string>() }));

            await converted.Download(new Archive { Name = "DeadlyStream Test.zip" }, filename.Path);

            Assert.Equal("eSIyd+KOG3s=", filename.FileHash());

            Assert.Equal(File.ReadAllText(filename.Path), "Cheese for Everyone!");
        }*/

        [Fact]
        public async Task GameFileSourceDownload()
        {
            // Test mode off for this test
            Consts.TestMode = false;
            var ini = $@"[General]
                        gameName={Game.SkyrimSpecialEdition.MetaData().MO2ArchiveName}
                        gameFile=Data/Update.esm";

            var state = (AbstractDownloadState)await DownloadDispatcher.ResolveArchive(ini.LoadIniString());

            Assert.NotNull(state);

            var converted = RoundTripState(state);
            Assert.True(await converted.Verify(new Archive(state: null!) { Size = 20}));
            await using var filename = new TempFile();

            Assert.True(converted.IsWhitelisted(new ServerWhitelist { AllowedPrefixes = new List<string>() }));

            await converted.Download(new Archive(state: null!) { Name = "Update.esm" }, filename.Path);

            Assert.Equal(Hash.FromBase64("/DLG/LjdGXI="), await filename.Path.FileHashAsync());
            Assert.Equal(await filename.Path.ReadAllBytesAsync(), await Game.SkyrimSpecialEdition.MetaData().GameLocation().Combine("Data/Update.esm").ReadAllBytesAsync());
            Consts.TestMode = true;
        }
        
        /// <summary>
        /// Tests that files from different sources don't overwrite eachother when downloaded by AInstaller
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task DownloadRenamingTests()
        {
            // Test mode off for this test
            Consts.TestMode = false;

            
            var inia = $@"[General]
                        gameName={Game.SkyrimSpecialEdition.MetaData().MO2ArchiveName}
                        gameFile=Data/Update.esm";

            var statea = (GameFileSourceDownloader.State)await DownloadDispatcher.ResolveArchive(inia.LoadIniString());

            var inib = $@"[General]
                        gameName={Game.SkyrimSpecialEdition.MetaData().MO2ArchiveName}
                        gameFile=Data/Skyrim.esm";
            var stateb = (GameFileSourceDownloader.State)await DownloadDispatcher.ResolveArchive(inib.LoadIniString());

            var archivesa = new List<Archive>()
            {
                new Archive(statea) {Hash = statea.Hash, Name = "Download.esm" }
            };

            var archivesb = new List<Archive>()
            {
                new Archive(stateb) {Hash = stateb.Hash, Name = "Download.esm" }
            };

            var folder = ((RelativePath)"DownloadTests").RelativeToEntryPoint();
            await folder.DeleteDirectory();
            folder.CreateDirectory();
           
            var inst = new TestInstaller(default, new ModList {GameType = Game.SkyrimSpecialEdition}, default, folder, null);

            await inst.DownloadMissingArchives(archivesa, true);
            await inst.DownloadMissingArchives(archivesb, true);

            Assert.Equal(new[]
                {
                    (RelativePath)@"Download.esm",
                    (RelativePath)@"Download_c4047f2251d8eead22df4b4888cc4b833ae7d9a6766ff29128e083d944f9ec4b_.esm",
                }.OrderBy(a => a).ToArray(),
            folder.EnumerateFiles().Select(f => f.FileName).OrderBy(a => a).ToArray());
           
            Consts.TestMode = true;
            
        }

        private T RoundTripState<T>(T state)
        {
            return state.ToJson().FromJsonString<T>();
        }
        
        /* TODO : Disabled for now
        [Fact]
        public async Task TestUpgrading()
        {
            await using var folder = await TempFolder.Create();
            var dest = folder.Dir.Combine("Cori.7z");
            var archive = new Archive(
                new NexusDownloader.State
                {
                    Game = Game.SkyrimSpecialEdition,
                    ModID = 24808,
                    FileID = 123501
                })
            {
                Name = "Cori.7z",
                Hash = Hash.FromBase64("gCRVrvzDNH0="),
            };
            Utils.Log($"Getting Hash for {(long)archive.Hash}");
            Assert.True(await DownloadDispatcher.DownloadWithPossibleUpgrade(archive, dest));
            Assert.Equal(Hash.FromBase64("gCRVrvzDNH0="), await dest.FileHashCachedAsync());
        }*/
        
        class TestInstaller : AInstaller
        {
            public TestInstaller(AbsolutePath archive, ModList modList, AbsolutePath outputFolder, AbsolutePath downloadFolder, SystemParameters parameters)
                : base(archive, modList, outputFolder, downloadFolder, parameters, steps: 1, modList.GameType)
            {
                DesiredThreads.OnNext(1);
            }

            protected override Task<bool> _Begin(CancellationToken cancel)
            {
                throw new NotImplementedException();
            }

            public override ModManager ModManager { get => ModManager.MO2; }
        }
    }
}
