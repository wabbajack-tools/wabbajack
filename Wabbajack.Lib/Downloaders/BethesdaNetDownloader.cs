using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reactive;
using System.Reactive.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using ReactiveUI;
using Wabbajack.Common;
using Wabbajack.Lib.Validation;
using File = Alphaleonis.Win32.Filesystem.File;
using Game = Wabbajack.Common.Game;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace Wabbajack.Lib.Downloaders
{
    public class BethesdaNetDownloader : IUrlDownloader, INeedsLogin
    {
        public const string DataName = "bethesda-net-data";
        public BethesdaNetDownloader()
        {
            TriggerLogin = ReactiveCommand.CreateFromTask(() => Utils.CatchAndLog(RequestLoginAndCache), IsLoggedIn.Select(b => !b).ObserveOn(RxApp.MainThreadScheduler));
            ClearLogin = ReactiveCommand.Create(() => Utils.DeleteEncryptedJson(DataName), IsLoggedIn.ObserveOn(RxApp.MainThreadScheduler));
        }

        private static async Task RequestLoginAndCache()
        {
            var result = await Utils.Log(new RequestBethesdaNetLogin()).Task;
        }

        public async Task<AbstractDownloadState> GetDownloaderState(dynamic archiveINI)
        {
            var url = (Uri)DownloaderUtils.GetDirectURL(archiveINI);
            return StateFromUrl(url);
        }

        internal static AbstractDownloadState StateFromUrl(Uri url)
        {
            if (url != null && url.Host == "bethesda.net" && url.AbsolutePath.StartsWith("/en/mods/"))
            {
                var split = url.AbsolutePath.Split('/');
                var game = split[3];
                var modId = split[5];
                return new State {GameName = game, ContentId = modId};
            }
            return null;
        }

        public async Task Prepare()
        {
            if (Utils.HaveEncryptedJson(DataName)) return;
            await Utils.Log(new RequestBethesdaNetLogin()).Task;
        }

        public static async Task<BethesdaNetData> Login(Game game)
        {
            var metadata = game.MetaData();
            var gamePath = Path.Combine(metadata.GameLocation(), metadata.MainExecutable);
            var info = new ProcessStartInfo
            {
                FileName = @"Downloaders\BethesdaNet\bethnetlogin.exe",
                Arguments = $"\"{gamePath}\" {metadata.MainExecutable}",
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            var process = Process.Start(info);
            ChildProcessTracker.AddProcess(process);
            string last_line = "";

            while (true)
            {
                var line = await process.StandardOutput.ReadLineAsync();
                if (line == null) break;
                last_line = line;
            }

            try
            {
                var result = last_line.FromJSONString<BethesdaNetData>();
                result.ToEcryptedJson(DataName);
                return result;
            }
            catch (Exception _)
            {
                return null;
            }
        }

        public AbstractDownloadState GetDownloaderState(string url)
        {
            return StateFromUrl(new Uri(url));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public ReactiveCommand<Unit, Unit> TriggerLogin { get; }
        public ReactiveCommand<Unit, Unit> ClearLogin { get; }
        public IObservable<bool> IsLoggedIn => Utils.HaveEncryptedJsonObservable(DataName);
        public string SiteName => "Bethesda.NET";
        public IObservable<string> MetaInfo => Observable.Return(""); //"Wabbajack will start the game, then exit once you enter the Mods page";
        public Uri SiteURL => new Uri("https://bethesda.net");
        public Uri IconUri { get; }

        public class State : AbstractDownloadState
        {
            public string GameName { get; set; }
            public string ContentId { get; set; }
            public override object[] PrimaryKey => new object[] {GameName, ContentId};

            public override bool IsWhitelisted(ServerWhitelist whitelist)
            {
                return true;
            }

            public override async Task<bool> Download(Archive a, string destination)
            {
                var (client, info, collected) = await ResolveDownloadInfo();
                using var tf = new TempFile();
                await using var file = tf.File.Create();
                var max_chunks = info.depot_list[0].file_list[0].chunk_count;
                foreach (var chunk in info.depot_list[0].file_list[0].chunk_list.OrderBy(c => c.index))
                {
                    Utils.Status($"Downloading {a.Name}", Percent.FactoryPutInRange(chunk.index, max_chunks));
                    using var got = await client.GetAsync(
                        $"https://content.cdp.bethesda.net/{collected.CDPProductId}/{collected.CDPPropertiesId}/{chunk.sha}");
                    var data = await got.Content.ReadAsByteArrayAsync();
                    if (collected.AESKey != null) 
                        AESCTRDecrypt(collected.AESKey, collected.AESIV, data);

                    if (chunk.uncompressed_size == chunk.chunk_size)
                        await file.WriteAsync(data, 0, data.Length);
                    else
                    {
                        using (var ms = new MemoryStream(data))
                        using (var zlibStream =
                            new ICSharpCode.SharpZipLib.Zip.Compression.Streams.InflaterInputStream(ms))
                            await zlibStream.CopyToAsync(file);
                    }
                }
                file.Close();
                await ConvertCKMToZip(file.Name, destination);

                return true;
            }


            private const uint CKM_Magic = 0x52415442; // BTAR
            private async Task ConvertCKMToZip(string src, string dest)
            {
                using var reader = new BinaryReader(File.OpenRead(src));
                var magic = reader.ReadUInt32();
                if (magic != CKM_Magic)
                    throw new InvalidDataException("Invalid magic format in CKM parsing");

                ushort majorVersion = reader.ReadUInt16();
                ushort minorVersion = reader.ReadUInt16();
                if (majorVersion != 1)
                    throw new InvalidDataException("Archive major version is unknown. Should be 1.");

                if (minorVersion < 2 || minorVersion > 4)
                    throw new InvalidDataException("Archive minor version is unknown. Should be 2, 3, or 4.");

                await using var fos = File.Create(dest);
                using var archive = new ZipArchive(fos, ZipArchiveMode.Create);
                while (reader.PeekChar() != -1)
                {
                    ushort nameLength = reader.ReadUInt16();
                    string name = Encoding.UTF8.GetString(reader.ReadBytes(nameLength));
                    ulong dataLength = reader.ReadUInt64();

                    if (dataLength > int.MaxValue)
                        throw new Exception();

                    var entry = archive.CreateEntry(name, CompressionLevel.NoCompression);
                    await using var output = entry.Open();
                    await reader.BaseStream.CopyToLimitAsync(output, (long)dataLength);
                }
            }

            public override async Task<bool> Verify(Archive archive)
            {
                var info = await ResolveDownloadInfo();
                return true;
            }

            private async Task<(Common.Http.Client, CDPTree, CollectedBNetInfo)> ResolveDownloadInfo()
            {
                var info = new CollectedBNetInfo();

                var login_info = Utils.FromEncryptedJson<BethesdaNetData>(DataName);

                var client = new Common.Http.Client();

                client.Headers.Add(("User-Agent", "bnet"));
                foreach (var header in login_info.headers.Where(h => h.Key.ToLower().StartsWith("x-")))
                    client.Headers.Add((header.Key, header.Value));

                var posted = await client.PostAsync("https://api.bethesda.net/beam/accounts/external_login",
                    new StringContent(login_info.body, Encoding.UTF8, "application/json"));

                info.AccessToken = (await posted.Content.ReadAsStringAsync()).FromJSONString<BeamLoginResponse>().access_token;

                client.Headers.Add(("x-cdp-app", "UGC SDK"));
                client.Headers.Add(("x-cdp-app-ver", "0.9.11314/debug"));
                client.Headers.Add(("x-cdp-lib-ver", "0.9.11314/debug"));
                client.Headers.Add(("x-cdp-platform","Win/32"));

                posted = await client.PostAsync("https://api.bethesda.net/cdp-user/auth",
                    new StringContent("{\"access_token\": \"" + info.AccessToken + "\"}", Encoding.UTF8,
                        "application/json"));
                info.CDPToken = (await posted.Content.ReadAsStringAsync()).FromJSONString<CDPLoginResponse>().token;

                client.Headers.Add(("X-Access-Token", info.AccessToken));
                var got = await client.GetAsync($"https://api.bethesda.net/mods/ugc-workshop/content/get?content_id={ContentId}");
                JObject data = JObject.Parse(await got.Content.ReadAsStringAsync());

                var content = data["platform"]["response"]["content"];

                info.CDPBranchId = (int)content["cdp_branch_id"];
                info.CDPProductId = (int)content["cdp_product_id"];

                client.Headers.Add(("Authorization", $"Token {info.CDPToken}"));
                client.Headers.Add(("Accept", "application/json"));

                got.Dispose();
                got = await client.GetAsync(
                    $"https://api.bethesda.net/cdp-user/projects/{info.CDPProductId}/branches/{info.CDPBranchId}/tree/.json");

                var tree = (await got.Content.ReadAsStringAsync()).FromJSONString<CDPTree>();
                
                got.Dispose();
                got = await client.PostAsync($"https://api.bethesda.net/mods/ugc-content/add-subscription", new StringContent($"{{\"content_id\": \"{ContentId}\"}}", Encoding.UTF8, "application/json"));

                got.Dispose();
                got = await client.GetAsync(
                    $"https://api.bethesda.net/cdp-user/projects/{info.CDPProductId}/branches/{info.CDPBranchId}/depots/.json");

                var props_obj = JObject.Parse(await got.Content.ReadAsStringAsync()).Properties().First();
                info.CDPPropertiesId = (int)props_obj.Value["properties_id"];
                
                info.AESKey = props_obj.Value["ex_info_A"].Select(e => (byte)e).ToArray();
                info.AESIV = props_obj.Value["ex_info_B"].Select(e => (byte)e).Take(16).ToArray();

                return (client, tree, info);
            }
            
            static int AESCTRDecrypt(byte[] Key, byte[] IV, byte[] Data)
            {
                IBufferedCipher cipher = CipherUtilities.GetCipher("AES/CTR/NoPadding");
                cipher.Init(false, new ParametersWithIV(ParameterUtilities.CreateKeyParameter("AES", Key), IV));

                return cipher.DoFinal(Data, Data, 0);
            }

            public override IDownloader GetDownloader()
            {
                return DownloadDispatcher.GetInstance<BethesdaNetDownloader>();
            }

            public override string GetManifestURL(Archive a)
            {
                return $"https://bethesda.net/en/mods/{GameName}/mod-detail/{ContentId}";
            }

            public override string[] GetMetaIni()
            {
                return new[] {"[General]", $"directURL=https://bethesda.net/en/mods/{GameName}/mod-detail/{ContentId}"};
            }


            private class BeamLoginResponse
            {
                public string access_token { get; set; }

            }

            private class CDPLoginResponse
            {
                public string token { get; set; }
            }

            private class CollectedBNetInfo
            {
                public byte[] AESKey { get; set; }
                public byte[] AESIV { get; set; }
                public string AccessToken { get; set; }
                public string CDPToken { get; set; }
                public int CDPBranchId { get; set; }
                public int CDPProductId { get; set; }
                public int CDPPropertiesId { get; set; }
            }

            public class CDPTree
            {
                public List<Depot> depot_list { get; set; }

                public class Depot
                {
                    public List<CDPFile> file_list { get; set; }

                    public class CDPFile
                    {
                        public int chunk_count { get; set; }
                        public List<Chunk> chunk_list { get; set; }

                        public string name { get; set; }

                        public class Chunk
                        {
                            public int chunk_size { get; set; }
                            public int index { get; set; }
                            public string sha { get; set; }
                            public int uncompressed_size { get; set; }
                        }
                    }
                }
            }

        }
    }

    internal class DownloadInfo
    {
    }

    public class RequestBethesdaNetLogin : AUserIntervention
    {
        public override string ShortDescription => "Logging into Bethesda.NET";
        public override string ExtendedDescription { get; }

        private readonly TaskCompletionSource<BethesdaNetData> _source = new TaskCompletionSource<BethesdaNetData>();
        public Task<BethesdaNetData> Task => _source.Task;

        public void Resume(BethesdaNetData data)
        {
            Handled = true;
            _source.SetResult(data);
        }

        public override void Cancel()
        {
            Handled = true;
            _source.SetCanceled();
        }

    }

    public class BethesdaNetData
    {
        public string body { get; set; }
        public Dictionary<string, string> headers = new Dictionary<string, string>();
    }

}
