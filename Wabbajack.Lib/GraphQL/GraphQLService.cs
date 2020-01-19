using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using GraphQL.Client;
using GraphQL.Client.Http;
using GraphQL.Common.Request;
using Wabbajack.Common;
using Wabbajack.Lib.FileUploader;
using Wabbajack.Lib.GraphQL.DTOs;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace Wabbajack.Lib.GraphQL
{
    public class GraphQLService
    {
        public static readonly Uri BaseURL = new Uri("https://build.wabbajack.org/graphql");
        
        public static async Task<List<UploadedFile>> GetUploadedFiles()
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

    }
}
