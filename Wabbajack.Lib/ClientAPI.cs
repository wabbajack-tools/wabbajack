﻿using System;
using System.Collections.Generic;
 using System.Linq;
 using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
 using Org.BouncyCastle.Crypto.Agreement.Srp;
 using Wabbajack.Common;
using Wabbajack.Common.Exceptions;
using Wabbajack.Common.Serialization.Json;
using Wabbajack.Lib.Downloaders;
 using Wabbajack.VirtualFileSystem;

 namespace Wabbajack.Lib
{
    [JsonName("ModUpgradeRequest")]
    public class ModUpgradeRequest
    {
        public Archive OldArchive { get; set; }
        public Archive NewArchive { get; set; }

        public ModUpgradeRequest(Archive oldArchive, Archive newArchive)
        {
            OldArchive = oldArchive;
            NewArchive = newArchive;
        }

        public async Task<bool> IsValid()
        {
            if (OldArchive.Size > 2_500_000_000 || NewArchive.Size > 2_500_000_000) return false;
            if (OldArchive.Hash == NewArchive.Hash && OldArchive.State.PrimaryKeyString == NewArchive.State.PrimaryKeyString) return false;
            if (OldArchive.State.GetType() != NewArchive.State.GetType())
                return false;
            if (OldArchive.State is IUpgradingState u)
            {
                return await u.ValidateUpgrade(OldArchive.Hash, NewArchive.State);
            }

            return false;
        }
    }
    
    public class ClientAPI
    {
        public static async Task<Wabbajack.Lib.Http.Client> GetClient()
        {
            var client = new Wabbajack.Lib.Http.Client();
            client.Headers.Add((Consts.MetricsKeyHeader, await Metrics.GetMetricsKey()));
            return client;
        }

        public static async Task<Uri> GetModUpgrade(Archive oldArchive, Archive newArchive, TimeSpan? maxWait = null, TimeSpan? waitBetweenTries = null, bool useAuthor = false)
        {
            maxWait ??= TimeSpan.FromMinutes(10);
            waitBetweenTries ??= TimeSpan.FromSeconds(15);
            
            var request = new ModUpgradeRequest( oldArchive, newArchive);
            var start = DateTime.UtcNow;
            
            RETRY:
            
            var response = await (useAuthor ? await AuthorApi.Client.GetAuthorizedClient() : await GetClient())
                .PostAsync($"{Consts.WabbajackBuildServerUri}mod_upgrade", new StringContent(request.ToJson(), Encoding.UTF8, "application/json"));

            if (response.IsSuccessStatusCode)
            {
                switch (response.StatusCode)
                {
                    case HttpStatusCode.OK:
                        return new Uri(await response.Content.ReadAsStringAsync());
                    case HttpStatusCode.Accepted:
                        Utils.Log($"Waiting for patch processing on the server for {oldArchive.Name}, sleeping for another 15 seconds");
                        await Task.Delay(TimeSpan.FromSeconds(15));
                        response.Dispose();
                        if (DateTime.UtcNow - start > maxWait)
                            throw new HttpException(response);
                        goto RETRY;
                }
            }
            var ex = new HttpException(response);
            response.Dispose();
            throw ex;
        }

        public class NexusCacheStats
        {
            public long CachedCount { get; set; }
            public long ForwardCount { get; set; }
            public double CacheRatio { get; set; }
        }

        public static async Task<NexusCacheStats> GetNexusCacheStats()
        {
            return await (await GetClient())
                .GetJsonAsync<NexusCacheStats>($"{Consts.WabbajackBuildServerUri}nexus_cache/stats");
        }

        public static async Task SendModListDefinition(ModList modList)
        {
            var client = await GetClient();
            await client.PostAsync($"{Consts.WabbajackBuildServerUri}list_definitions/ingest", new StringContent(modList.ToJson(), Encoding.UTF8, "application/json"));
        }

        public static async Task<Archive[]> GetExistingGameFiles(WorkQueue queue, Game game)
        {
            var client = await GetClient();
            var metaData = game.MetaData();
            var results =
                await client.GetJsonAsync<Archive[]>(
                    $"{Consts.WabbajackBuildServerUri}game_files/{game}/{metaData.InstalledVersion}");

            return (await results.PMap(queue, async file => (await file.State.Verify(file), file))).Where(f => f.Item1)
                .Select(f =>
                {
                    f.file.Name = ((GameFileSourceDownloader.State)f.file.State).GameFile.Munge().ToString();
                    return f.file;
                })
                .ToArray();
        }

        public static async Task<AbstractDownloadState?> InferDownloadState(Hash hash)
        {
            var client = await GetClient();
            var results = await client.GetJsonAsync<Archive[]>($"{Consts.WabbajackBuildServerUri}mod_files/by_hash/{hash.ToHex()}");

            await DownloadDispatcher.PrepareAll(results.Select(r => r.State));
            foreach (var result in results)
            {
                try
                {
                    if (await result.State.Verify(result)) return result.State;
                }
                catch (Exception ex)
                {
                    Utils.Log($"Verification error for failed for inferenced archive {result.State.PrimaryKeyString}");
                    Utils.Log(ex.ToString());
                }
            }
            return null;
        }

        public static async Task<Archive[]> GetModUpgrades(Hash src)
        {
            var client = await GetClient();
            Utils.Log($"Looking for generic upgrade for {src} ({(long)src})");
            var results = await client.GetJsonAsync<Archive[]>($"{Consts.WabbajackBuildServerUri}mod_upgrade/find/{src.ToHex()}");
            return results;
        }

        public static async Task<VirusScanner.Result> GetVirusScanResult(AbsolutePath path)
        {
            var client = await GetClient();
            Utils.Log($"Checking virus result for {path}");

            var hash = await path.FileHashAsync();

            using var result = await client.GetAsync($"{Consts.WabbajackBuildServerUri}virus_scan/{hash.ToHex()}", errorsAsExceptions: false);
            if (result.StatusCode == HttpStatusCode.OK)
            {
                var data = await result.Content.ReadAsStringAsync();
                return Enum.Parse<VirusScanner.Result>(data);
            }

            if (result.StatusCode == HttpStatusCode.NotFound)
            {
                await using var input = await path.OpenRead();
                using var postResult = await client.PostAsync($"{Consts.WabbajackBuildServerUri}virus_scan", errorsAsExceptions: false, content: new StreamContent(input));
                if (postResult.StatusCode == HttpStatusCode.OK)
                {
                    var data = await postResult.Content.ReadAsStringAsync();
                    return Enum.Parse<VirusScanner.Result>(data);
                }
                throw new HttpException(result);
            }
            throw new HttpException(result);
        }
    }
}
