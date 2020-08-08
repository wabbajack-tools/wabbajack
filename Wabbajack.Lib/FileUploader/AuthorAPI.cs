using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Lib.Downloaders;
using File = Alphaleonis.Win32.Filesystem.File;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace Wabbajack.Lib.FileUploader
{
    public class AuthorAPI
    {
        public static IObservable<bool> HaveAuthorAPIKey => Utils.HaveEncryptedJsonObservable(Consts.AuthorAPIKeyFile);

        public static string? ApiKeyOverride = null;

        public static async Task<string> GetAPIKey(string? apiKey = null)
        {
            if (ApiKeyOverride != null) return ApiKeyOverride;
            return apiKey ?? (await Consts.LocalAppDataPath.Combine(Consts.AuthorAPIKeyFile).ReadAllTextAsync()).Trim();
        }
 
        public static async Task<Wabbajack.Lib.Http.Client> GetAuthorizedClient(string? apiKey = null)
        {
            var client = new Wabbajack.Lib.Http.Client();
            client.Headers.Add(("X-API-KEY", await GetAPIKey(apiKey)));
            return client;
        }
        
        public static async Task<string> RunJob(string jobtype)
        {
            var client = await GetAuthorizedClient();
            return await client.GetStringAsync($"{Consts.WabbajackBuildServerUri}jobs/enqueue_job/{jobtype}");
        }

        public static async Task<string> UpdateNexusCache()
        {
            return await RunJob("GetNexusUpdatesJob");
        }

        public static async Task<string> UpdateServerModLists()
        {
            return await RunJob("UpdateModLists");
        }

        public static async Task<string> GetServerLog()
        {
            return await (await GetAuthorizedClient()).GetStringAsync($"{Consts.WabbajackBuildServerUri}heartbeat/logs");
        }

        public static async Task<IEnumerable<string>> GetMyFiles()
        {
            return (await (await GetAuthorizedClient()).GetStringAsync($"{Consts.WabbajackBuildServerUri}uploaded_files/list")).FromJsonString<string[]>();
        }

        public static async Task<string> DeleteFile(string name)
        {
            var result = await (await GetAuthorizedClient())
                .DeleteStringAsync($"{Consts.WabbajackBuildServerUri}uploaded_files/{name}");
            return result;
        }

        public static async Task<string> PurgeNexusModInfo(long modId)
        {
            return await (await GetAuthorizedClient()).GetStringAsync($"{Consts.WabbajackBuildServerUri}purge_nexus_cache/{modId}");
        }

        public static async Task<string> NoPatch(Hash hash, string rationale)
        {
            var client = await GetAuthorizedClient();
            return await client.GetStringAsync(
                $"{Consts.WabbajackBuildServerUri}mod_upgrade/no_patch/{hash.ToHex()}/{Encoding.UTF8.GetBytes(rationale).ToHex()}");
        }
    }
}
