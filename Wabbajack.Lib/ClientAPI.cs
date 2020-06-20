﻿using System;
using System.Collections.Generic;
 using System.Linq;
 using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
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

        public bool IsValid
        {
            get
            {
                if (OldArchive.Size > 2_500_000_000 || NewArchive.Size > 2_500_000_000) return false;
                if (OldArchive.Hash == NewArchive.Hash && OldArchive.State.PrimaryKeyString == NewArchive.State.PrimaryKeyString) return false;
                if (OldArchive.State.GetType() != NewArchive.State.GetType())
                    return false;
                if (OldArchive.State is IUpgradingState u)
                {
                    return u.ValidateUpgrade(NewArchive.State);
                }

                return false;
            }
        }
    }
    
    public class ClientAPI
    {
        public static async Task<Common.Http.Client> GetClient()
        {
            var client = new Common.Http.Client();
            client.Headers.Add((Consts.MetricsKeyHeader, await Metrics.GetMetricsKey()));
            return client;
        }

        public static async Task<Uri> GetModUpgrade(Archive oldArchive, Archive newArchive, TimeSpan? maxWait = null, TimeSpan? waitBetweenTries = null)
        {
            maxWait ??= TimeSpan.FromMinutes(10);
            waitBetweenTries ??= TimeSpan.FromSeconds(15);
            
            var request = new ModUpgradeRequest( oldArchive, newArchive);
            var start = DateTime.UtcNow;
            
            RETRY:
            
            var response = await (await GetClient())
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

        /// <summary>
        /// Given an archive hash, search the Wabbajack server for a matching .ini file
        /// </summary>
        /// <param name="hash"></param>
        /// <returns></returns>
        public static async Task<string?> GetModIni(Hash hash)
        {
            var client = new Common.Http.Client();
            try
            {
                return await client.GetStringAsync(
                        $"{Consts.WabbajackBuildServerUri}indexed_files/{hash.ToHex()}/meta.ini");
            }
            catch (HttpException)
            {
                return null;
            }
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
    }
}
