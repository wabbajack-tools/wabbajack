using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using GraphQL.Client;
using GraphQL.Client.Http;
using GraphQL.Common.Request;
using Wabbajack.Common;
using Wabbajack.Lib.GraphQL.DTOs;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace Wabbajack.Lib.GraphQL
{
    public class GraphQLService
    {
        public static readonly Uri BaseURL = new Uri("https://build.wabbajack.org/graphql");
        public static readonly Uri UploadURL = new Uri("https://build.wabbajack.org/upload_file");
        
        public async Task<List<UploadedFile>> GetUploadedFiles()
        {
            var client = new GraphQLHttpClient(BaseURL);
            var query = new GraphQLRequest
            {
                Query = @"
                   query uploadedFilesQuery {
                         uploadedFiles {
                            id
                            name
                            hash
                            uri
                            uploader
                            uploadDate
                          }
                   }"
            };
            var result = await client.SendQueryAsync(query);
            return result.GetDataFieldAs<List<UploadedFile>>("uploadedFiles");
        }

        public async Task<bool> UploadFile(string filename)
        {
            using (var stream = new StatusFileStream(File.OpenRead(filename), $"Uploading {Path.GetFileName(filename)}"))
            {
                var client = new HttpClient();
                client.DefaultRequestHeaders.Add("X-API-KEY", "TODO");
                var form = new MultipartFormDataContent {{new StreamContent(stream), "file"}};
                var response = await client.PostAsync(UploadURL, form);
                return response.IsSuccessStatusCode;
            }
        }
    }
}
