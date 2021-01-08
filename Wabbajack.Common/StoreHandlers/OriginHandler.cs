using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Wabbajack.Common.StoreHandlers
{
    public class OriginHandler : AStoreHandler
    {
        private AbsolutePath OriginDataPath = (AbsolutePath)@"C:\ProgramData\Origin\LocalContent";
        private Extension MFSTExtension = new Extension(".mfst");
        private HashSet<string> KnownMFSTs = new();

        public override StoreType Type { get; internal set; } = StoreType.Origin;

        private static Regex SplitRegex = new Regex("[0-9]+");
        public override bool Init()
        {
            try
            {
                if (!OriginDataPath.Exists)
                    return false;

                KnownMFSTs = OriginDataPath.EnumerateFiles()
                    .Where(f => f.Extension == MFSTExtension)
                    .Select(f => f.FileNameWithoutExtension.ToString())
                    .Select(f =>
                    {
                        var result = SplitRegex.Match(f);
                        if (result == null) return default;
                        var a = f.Substring(0, result.Index);
                        var b = f.Substring(result.Index);
                        return a + ":" + b;
                    })
                    .Where(t => t != default)
                    .Select(t => t!)
                    .Where(t => !t.Contains("."))
                    .ToHashSet();

                foreach (var known in KnownMFSTs)
                {
                    try
                    {
                        var resp = OriginGame.GetAndCacheManifestResponse(known)
                            .FromJsonString<OriginGame.GameLocalDataResponse>();
                        Utils.Log($"Found Origin Content {resp.localizableAttributes!.displayName} ({known})");
                    }
                    catch (Exception ex)
                    {
                        Utils.Log($"Origin got {ex.Message} when loading info for {known}");
                        continue;
                    }

                }

                Utils.Log($"Found MFSTs from Origin: {string.Join(", ", KnownMFSTs)}");

                return true;

            }
            catch (Exception)
            {
                return false;
            }
        }

        public override bool LoadAllGames()
        {
            try
            {
                
                
                foreach (var game in GameRegistry.Games)
                {
                    var mfst = game.Value.OriginIDs.FirstOrDefault(g => KnownMFSTs.Contains(g.Replace(":", "")));
                    if (mfst == null)
                        continue;

                    var ogame = new OriginGame(mfst, game.Key, game.Value);
                    Games.Add(ogame);
                }

                return true;
            }
            catch (Exception ex)
            {
                Utils.Log(ex.ToString());
                return false;
            }
        }
    }
    
    public sealed class OriginGame : AStoreGame
    {
        private string _mfst;
        private GameMetaData _metaData;

        public OriginGame(string mfst, Game game, GameMetaData metaData)
        {
            _mfst = mfst;
            Game = game;
            _metaData = metaData;
        }
        public override Game Game { get; internal set; }
        public override StoreType Type { get; internal set; } = StoreType.Origin;

        public override AbsolutePath Path
        {
            get
            {
                return GetGamePath();

            }
            internal set
            {
                throw new NotImplementedException();
            }
        }

        private AbsolutePath GetGamePath()
        {
            var manifestData = GetAndCacheManifestResponse(this._mfst).FromJsonString<GameLocalDataResponse>();
            var platform = manifestData!.publishing!.softwareList!.software!.FirstOrDefault(a => a.softwarePlatform == "PCWIN");
            
            var installPath = GetPathFromPlatformPath(platform!.fulfillmentAttributes!.installCheckOverride!);
            return installPath;

        }
        
        internal AbsolutePath GetPathFromPlatformPath(string path, RegistryView platformView)
        {
            if (!path.StartsWith("["))
            {
                return  (AbsolutePath)path;
            }

            var matchPath = Regex.Match(path, @"\[(.*?)\\(.*)\\(.*)\](.*)");
            if (!matchPath.Success)
            {
                Utils.Log("Unknown path format " + path);
                return default;
            }

            var root = matchPath.Groups[1].Value;

            RegistryKey rootKey = root switch
            {
                "HKEY_LOCAL_MACHINE" => RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, platformView),
                "HKEY_CURRENT_USER" => RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, platformView),
                _ => throw new Exception("Unknown registry root entry " + root)
            };

            var subPath = matchPath.Groups[2].Value.Trim('\\');
            var key = matchPath.Groups[3].Value;
            var executable = matchPath.Groups[4].Value.Trim('\\');
            var subKey = rootKey.OpenSubKey(subPath);
            if (subKey == null)
            {
                return default;
            }

            var keyValue = rootKey!.OpenSubKey(subPath)!.GetValue(key);
            if (keyValue == null)
            {
                return default;
            }

            return (AbsolutePath)keyValue!.ToString()!;
        }
        
        internal AbsolutePath GetPathFromPlatformPath(string path)
        {
            var resultPath = GetPathFromPlatformPath(path, RegistryView.Registry64);
            if (resultPath == default)
            {
                resultPath = GetPathFromPlatformPath(path, RegistryView.Registry32);
            }

            return resultPath;
        }

        private static AbsolutePath ManifestCacheLocation(string mfst) =>
            Consts.LocalAppDataPath.Combine("OriginManifestCache", mfst.Replace(":", ""));

        internal static string GetAndCacheManifestResponse(string mfst)
        {
            var location = ManifestCacheLocation(mfst);
            if (location.Exists)
            {
                return location.ReadAllText();
            }

            Utils.Log($"Getting Origin Manifest info for {mfst}");
            var client = new HttpClient();
            var data = client.GetStringAsync($"https://api1.origin.com/ecommerce2/public/{mfst}/en_US").Result;
            location.Parent.CreateDirectory();
            location.WriteAllTextAsync(data).Wait();
            return data;
        }

        public class GameLocalDataResponse
        {
            public class LocalizableAttributes
            {
                public string? longDescription;
                public string? displayName;
            }

            public class Publishing
            {
                public class Software
                {
                    public class FulfillmentAttributes
                    {
                        public string? executePathOverride;
                        public string? installationDirectory;
                        public string? installCheckOverride;
                    }

                    public string? softwareId;
                    public string? softwarePlatform;
                    public FulfillmentAttributes? fulfillmentAttributes;
                }

                public class SoftwareList
                {
                    public List<Software>? software;
                }

                public SoftwareList? softwareList;
            }

            public string? offerId;
            public string? offerType;
            public Publishing? publishing;
            public LocalizableAttributes? localizableAttributes;
        }
    }
}
