using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Wabbajack.Common;

namespace Wabbajack.Lib.AuthorApi
{
    public class Client
    {
        public static async Task<Client> Create(string? apiKey = null)
        {
            var client = await GetAuthorizedClient(apiKey);
            return new Client(client); 
        }

        private Client(Common.Http.Client client)
        {
            _client = client;
        }
        
        public static async Task<Common.Http.Client> GetAuthorizedClient(string? apiKey = null)
        {
            var client = new Common.Http.Client();
            client.Headers.Add(("X-API-KEY", await GetAPIKey(apiKey)));
            return client;
        }

        public static string? ApiKeyOverride = null;
        private Common.Http.Client _client;

        public static async ValueTask<string> GetAPIKey(string? apiKey = null)
        {
            return apiKey ?? (await Consts.LocalAppDataPath.Combine(Consts.AuthorAPIKeyFile).ReadAllTextAsync()).Trim();
        }


        public async Task<CDNFileDefinition> GenerateFileDefinition(WorkQueue queue, AbsolutePath path, Action<string, Percent> progressFn)
        {
            IEnumerable<CDNFilePartDefinition> Blocks(AbsolutePath path)
            {
                var size = path.Size;
                for (long block = 0; block * Consts.UPLOADED_FILE_BLOCK_SIZE < size; block ++)
                    yield return new CDNFilePartDefinition
                    {
                        Index = block,
                        Size = Math.Min(Consts.UPLOADED_FILE_BLOCK_SIZE, size - block * Consts.UPLOADED_FILE_BLOCK_SIZE),
                        Offset = block * Consts.UPLOADED_FILE_BLOCK_SIZE
                    };
            }
            
            var parts = Blocks(path).ToArray();
            var definition = new CDNFileDefinition
            {
                OriginalFileName = path.FileName, 
                Size = path.Size, 
                Hash = await path.FileHashCachedAsync(),
                Parts = await parts.PMap(queue, async part =>
                {
                    progressFn("Hashing file parts", Percent.FactoryPutInRange(part.Index, parts.Length));
                    var buffer = new byte[part.Size];
                    await using (var fs = path.OpenShared())
                    {
                        fs.Position = part.Offset;
                        await fs.ReadAsync(buffer);
                    }
                    part.Hash = buffer.xxHash();
                    return part;
                })
            };

            return definition;
        }

        public async Task<Uri> UploadFile(WorkQueue queue, AbsolutePath path, Action<string, Percent> progressFn)
        {
            var definition = await GenerateFileDefinition(queue, path, progressFn);

            using (var result = await _client.PutAsync($"{Consts.WabbajackBuildServerUri}authored_files/create",
                new StringContent(definition.ToJson())))
            {
                progressFn("Starting upload", Percent.Zero);
                definition.ServerAssignedUniqueId = await result.Content.ReadAsStringAsync();
            }

            var results = await definition.Parts.PMap(queue, async part =>
            {
                progressFn("Uploading Part", Percent.FactoryPutInRange(part.Index, definition.Parts.Length));
                var buffer = new byte[part.Size];
                await using (var fs = path.OpenShared())
                {
                    fs.Position = part.Offset;
                    await fs.ReadAsync(buffer);
                }

                int retries = 0;
                while (true)
                {
                    try
                    {
                        using var putResult = await _client.PutAsync(
                            $"{Consts.WabbajackBuildServerUri}authored_files/{definition.ServerAssignedUniqueId}/part/{part.Index}",
                            new ByteArrayContent(buffer));
                        var hash = Hash.FromBase64(await putResult.Content.ReadAsStringAsync());
                        if (hash != part.Hash)
                            throw new InvalidDataException("Hashes don't match");
                        return hash;
                    }
                    catch (Exception ex)
                    {
                        Utils.Log("Failure uploading part");
                        Utils.Log(ex.ToString());
                        if (retries <= 4)
                        {
                            retries++;
                            continue;
                        }
                        Utils.ErrorThrow(ex);
                    }
                }
            });
            
            progressFn("Finalizing upload", Percent.Zero);
            using (var result = await _client.PutAsync($"{Consts.WabbajackBuildServerUri}authored_files/{definition.ServerAssignedUniqueId}/finish",
                new StringContent(definition.ToJson())))
            {
                progressFn("Finished", Percent.One);
                return new Uri(await result.Content.ReadAsStringAsync());
            }
        }
    }
}
